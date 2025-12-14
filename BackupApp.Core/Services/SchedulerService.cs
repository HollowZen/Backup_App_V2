using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BackupApp.Core.Models;
using BackupApp.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace BackupApp.Core.Services;

public class SchedulerService : ISchedulerService, IDisposable
{
    private static readonly TimeSpan MainInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PrecisionInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PrecisionWindow = TimeSpan.FromSeconds(45);

    private readonly IBackupTaskRepository _repository;
    private readonly IBackupService _backupService;
    private readonly ILogger<SchedulerService> _logger;
    private readonly IRetentionService _retentionService;
    private readonly SemaphoreSlim _evaluationLock = new(1, 1);
    private readonly ConcurrentDictionary<int, DateTime> _lastRunByTask = new();
    private readonly ConcurrentDictionary<int, byte> _runningTasks = new();

    private readonly object _syncRoot = new();
    private Timer? _mainTimer;
    private Timer? _precisionTimer;
    private CancellationTokenSource? _cts;
    private bool _precisionEnabled;
    private bool _disposed;
    private bool _debugEnabled;

    public SchedulerService(IBackupTaskRepository repository, IBackupService backupService, IRetentionService retentionService, ILogger<SchedulerService> logger)
    {
        _repository = repository;
        _backupService = backupService;
        _retentionService = retentionService;
        _logger = logger;
    }

    public event EventHandler<SchedulerStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SchedulerTaskEventArgs>? TaskStarted;
    public event EventHandler<SchedulerTaskEventArgs>? TaskCompleted;

    public bool IsRunning { get; private set; }
    public bool IsPrecisionTimerActive => _precisionEnabled;
    public bool IsDebugEnabled => _debugEnabled;

    public void StartScheduler(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (IsRunning)
            {
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _mainTimer = new Timer(OnMainTimerTick, null, TimeSpan.Zero, MainInterval);
            IsRunning = true;
            PublishStatus("Планировщик запущен (30 c).", SchedulerTickType.Main);
        }
    }

    public void StopScheduler()
    {
        lock (_syncRoot)
        {
            if (!IsRunning)
            {
                return;
            }

            _cts?.Cancel();
            _mainTimer?.Dispose();
            _precisionTimer?.Dispose();
            _precisionEnabled = false;
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
            PublishStatus("Планировщик остановлен.", SchedulerTickType.Main);
        }
    }

    public void SetDebugMode(bool enabled)
    {
        _debugEnabled = enabled;
        PublishStatus(enabled ? "Отладочный режим включён." : "Отладочный режим выключен.", SchedulerTickType.Main);
    }

    public DateTime? CalculateNextRun(BackupTask task, DateTime? from = null)
    {
        if (task.ScheduleType == ScheduleType.Manual || string.IsNullOrWhiteSpace(task.ScheduleTime))
        {
            return null;
        }

        var pivot = from ?? DateTime.Now;

        if (!TimeSpan.TryParse(task.ScheduleTime, out var time))
        {
            return null;
        }

        var scheduledToday = new DateTime(pivot.Year, pivot.Month, pivot.Day, time.Hours, time.Minutes, 0);

        return task.ScheduleType switch
        {
            ScheduleType.Daily => scheduledToday >= pivot ? scheduledToday : scheduledToday.AddDays(1),
            ScheduleType.Weekly => CalculateNextWeekly(task, pivot, time),
            ScheduleType.Monthly => CalculateNextMonthly(task, pivot, time),
            _ => null
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopScheduler();
        _evaluationLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnMainTimerTick(object? state)
    {
        _ = EvaluateAsync(SchedulerTickType.Main);
    }

    private void OnPrecisionTimerTick(object? state)
    {
        _ = EvaluateAsync(SchedulerTickType.Precision);
    }

    private async Task EvaluateAsync(SchedulerTickType tickType)
    {
        if (!IsRunning || _cts is null)
        {
            return;
        }

        if (!await _evaluationLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var token = _cts.Token;
            var tasks = await _repository.GetAllAsync(token);
            var now = DateTime.Now;
            var referencePoint = now - PrecisionWindow;
            DateTime? nearest = null;

            foreach (var task in tasks.Where(t => t.IsEnabled))
            {
                var nextRun = CalculateNextRun(task, referencePoint);
                if (nextRun is null)
                {
                    continue;
                }

                if (nearest is null || nextRun < nearest)
                {
                    nearest = nextRun;
                }

                if (nextRun <= now && ShouldRun(task, nextRun.Value))
                {
                    _ = RunTaskAsync(task, nextRun.Value, token);
                }
            }

            UpdatePrecisionTimer(nearest, now);

            if (_debugEnabled)
            {
                PublishStatus(
                    nearest is null
                        ? $"[{tickType}] Активных расписаний нет."
                        : $"[{tickType}] ближайший запуск: {nearest.Value:G}",
                    tickType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении планировщика.");
            PublishStatus($"Ошибка планировщика: {ex.Message}", tickType);
        }
        finally
        {
            _evaluationLock.Release();
        }
    }

    private bool ShouldRun(BackupTask task, DateTime scheduledTime)
    {
        var lastRun = _lastRunByTask.GetOrAdd(task.Id, DateTime.MinValue);
        if (lastRun >= scheduledTime)
        {
            return false;
        }

        return _runningTasks.TryAdd(task.Id, 0);
    }

    private Task RunTaskAsync(BackupTask task, DateTime scheduledTime, CancellationToken token)
    {
        PublishTaskEvent(TaskStarted, task, scheduledTime, isSuccess: true);

        return Task.Run(async () =>
        {
            try
            {
                var freshTask = await _repository.GetByIdAsync(task.Id, token);
                if (freshTask is null)
                {
                    PublishTaskEvent(TaskCompleted, task, scheduledTime, isSuccess: false, error: "Задача не найдена.");
                    return;
                }

                var result = await _backupService.ExecuteBackupAsync(freshTask, null, token);
                var completionMoment = DateTime.UtcNow;
                var lastFull = result.PerformedBackupType == BackupType.Full ? completionMoment : (DateTime?)null;
                await _repository.UpdateLastRunAsync(freshTask.Id, completionMoment, lastFull, token);
                freshTask.LastBackupTime = completionMoment;
                if (lastFull.HasValue)
                {
                    freshTask.LastFullBackupTime = lastFull;
                }

                await _retentionService.ApplyRetentionPolicyAsync(freshTask.Id, token);
                _lastRunByTask[task.Id] = scheduledTime;
                PublishTaskEvent(TaskCompleted, freshTask, scheduledTime, isSuccess: true, result: result);
            }
            catch (OperationCanceledException)
            {
                PublishTaskEvent(TaskCompleted, task, scheduledTime, isSuccess: false, error: "Выполнение отменено.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка резервного копирования для задачи {Task}", task.Name);
                PublishTaskEvent(TaskCompleted, task, scheduledTime, isSuccess: false, error: ex.Message);
            }
            finally
            {
                _runningTasks.TryRemove(task.Id, out _);
            }
        }, CancellationToken.None);
    }

    private void UpdatePrecisionTimer(DateTime? nearest, DateTime now)
    {
        if (nearest is null)
        {
            DisablePrecisionTimer();
            return;
        }

        var delta = nearest.Value - now;
        if (delta <= PrecisionWindow && !_precisionEnabled)
        {
            EnablePrecisionTimer();
        }
        else if (delta > PrecisionWindow && _precisionEnabled)
        {
            DisablePrecisionTimer();
        }
    }

    private void EnablePrecisionTimer()
    {
        _precisionTimer?.Dispose();
        _precisionTimer = new Timer(OnPrecisionTimerTick, null, TimeSpan.Zero, PrecisionInterval);
        _precisionEnabled = true;
        PublishStatus("Включён точный таймер (5 c).", SchedulerTickType.Precision);
    }

    private void DisablePrecisionTimer()
    {
        if (!_precisionEnabled)
        {
            return;
        }

        _precisionTimer?.Dispose();
        _precisionTimer = null;
        _precisionEnabled = false;
        PublishStatus("Точный таймер отключён.", SchedulerTickType.Precision);
    }

    private void PublishStatus(string message, SchedulerTickType tickType)
    {
        StatusChanged?.Invoke(this, new SchedulerStatusChangedEventArgs(message, IsRunning, _precisionEnabled, tickType));
    }

    private void PublishTaskEvent(EventHandler<SchedulerTaskEventArgs>? handler, BackupTask task, DateTime scheduledTime, bool isSuccess, string? error = null, BackupExecutionResult? result = null)
    {
        handler?.Invoke(this, new SchedulerTaskEventArgs(task, scheduledTime, isSuccess, error, result));
    }

    private static DateTime? CalculateNextWeekly(BackupTask task, DateTime pivot, TimeSpan time)
    {
        if (string.IsNullOrWhiteSpace(task.ScheduleDays))
        {
            return null;
        }

        var days = task.ScheduleDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseDayOfWeek)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (days.Count == 0)
        {
            return null;
        }

        for (var offset = 0; offset <= 7; offset++)
        {
            var candidateDate = pivot.Date.AddDays(offset);
            if (!days.Contains(candidateDate.DayOfWeek))
            {
                continue;
            }

            var candidateTime = candidateDate.Add(time);
            if (candidateTime >= pivot)
            {
                return candidateTime;
            }
        }

        var nextWeek = pivot.Date.AddDays(7);
        return nextWeek.Add(time);
    }

    private static DateTime? CalculateNextMonthly(BackupTask task, DateTime pivot, TimeSpan time)
    {
        if (task.ScheduleDayOfMonth is null or <= 0)
        {
            return null;
        }

        var desiredDay = task.ScheduleDayOfMonth.Value;

        DateTime Build(int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var safeDay = Math.Min(desiredDay, daysInMonth);
            return new DateTime(year, month, safeDay, time.Hours, time.Minutes, 0);
        }

        var thisMonth = Build(pivot.Year, pivot.Month);
        if (thisMonth >= pivot)
        {
            return thisMonth;
        }

        var nextMonth = pivot.AddMonths(1);
        return Build(nextMonth.Year, nextMonth.Month);
    }

    private static DayOfWeek? ParseDayOfWeek(string value)
    {
        if (Enum.TryParse<DayOfWeek>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return value.ToLowerInvariant() switch
        {
            "mon" or "monday" or "пн" => DayOfWeek.Monday,
            "tue" or "tuesday" or "вт" => DayOfWeek.Tuesday,
            "wed" or "wednesday" or "ср" => DayOfWeek.Wednesday,
            "thu" or "thursday" or "чт" => DayOfWeek.Thursday,
            "fri" or "friday" or "пт" => DayOfWeek.Friday,
            "sat" or "saturday" or "сб" => DayOfWeek.Saturday,
            "sun" or "sunday" or "вс" => DayOfWeek.Sunday,
            _ => null
        };
    }
}
