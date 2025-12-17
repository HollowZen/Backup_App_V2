using BackupApp.Core.Constants;
using BackupApp.Core.Models;
using BackupApp.Core.Repositories;
using BackupApp.Core.Services;
using BackupApp.WPF.Commands;
using BackupApp.WPF.Helpers;
using BackupApp.WPF.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace BackupApp.WPF.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private readonly IBackupTaskRepository _repository;
    private readonly IBackupService _backupService;
    private readonly ISchedulerService _schedulerService;
    private readonly IRetentionService _retentionService;
    private readonly IMessageService _messageService;
    private readonly Func<TaskViewModel> _taskVmFactory;
    private readonly Func<DecryptWindowViewModel> _decryptWindowViewModelFactory;
    private readonly Dispatcher _dispatcher;

   private BackupTask? _selectedTask;
private bool _isBusy;
    private double _backupProgress;
    private bool _isBackupInProgress;
    private int _selectedTabIndex;
    private TaskViewModel? _currentTaskViewModel;
    private DecryptWindowViewModel? _currentDecryptViewModel;
    private bool _isEditingTask;
    private bool _isCreatingNewTask;

    public ObservableCollection<BackupTask> Tasks { get; } = new();
    public ObservableCollection<string> ExecutionLog { get; } = new();
    public ObservableCollection<BackupHistory> SelectedTaskHistory { get; } = new();

    public BackupTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                RefreshCommands();
                _ = LoadHistoryForSelectedTaskAsync();
                // Если мы на вкладке "Задача", синхронизируем данные
                if (SelectedTabIndex == 1 && value != null)
                {
                    SyncSelectedTaskToViewModel();
                }
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) RefreshCommands(); }
}

    public bool IsBackupInProgress
{
get => _isBackupInProgress;
        private set { if (SetProperty(ref _isBackupInProgress, value)) RefreshCommands(); }
    }

    public string StatusMessage { get; private set; } = "Готово";
    public string SchedulerStatus { get; private set; } = "Планировщик остановлен.";
    public bool IsSchedulerRunning { get; private set; }
    public bool IsPrecisionTimerActive { get; private set; }
    public string PrecisionTimerStatus { get; private set; } = "Точный таймер: выкл.";

    public double BackupProgress
{
       get => _backupProgress;
        private set => SetProperty(ref _backupProgress, value);
    }

    public string CurrentTransferFile { get; private set; } = "—";
    public string TransferSpeedText { get; private set; } = "—";
    public string RemainingTimeText { get; private set; } = "—";

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                // При переходе на вкладку "Задача" (индекс 1) синхронизируем выбранную задачу
                // Но только если мы не только что создали новую задачу
                if (value == 1 && !_isCreatingNewTask)
                {
                    SyncSelectedTaskToViewModel();
                }
            }
        }
    }

    public TaskViewModel? CurrentTaskViewModel
    {
        get => _currentTaskViewModel;
        private set
        {
            if (SetProperty(ref _currentTaskViewModel, value))
            {
                SaveTaskCommand.RaiseCanExecuteChanged();
                CancelTaskCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DecryptWindowViewModel? CurrentDecryptViewModel
    {
        get => _currentDecryptViewModel;
        private set => SetProperty(ref _currentDecryptViewModel, value);
    }

    public string TaskTabHeader => _isEditingTask ? "Редактирование задачи" : "Создание задачи";

    // ---------------- Commands ---------------- //

    public RelayCommand AddTaskCommand { get; }
    public RelayCommand DeleteTaskCommand { get; }
    public RelayCommand RunTaskCommand { get; }
    public RelayCommand DecryptBackupCommand { get; }
public RelayCommand StartSchedulerCommand { get; }
    public RelayCommand StopSchedulerCommand { get; }
    public RelayCommand SaveTaskCommand { get; }
    public RelayCommand CancelTaskCommand { get; }

    // ---------------- Constructor ---------------- //

    public MainWindowViewModel(
        IBackupTaskRepository repository,
        IBackupService backupService,
        ISchedulerService schedulerService,
        IRetentionService retentionService,
        IMessageService messageService,
        Func<TaskViewModel> taskVmFactory,
        Func<DecryptWindowViewModel> decryptWindowViewModelFactory)
    {
        _repository = repository;
        _backupService = backupService;
        _schedulerService = schedulerService;
        _retentionService = retentionService;
_messageService = messageService;
        _taskVmFactory = taskVmFactory;
        _decryptWindowViewModelFactory = decryptWindowViewModelFactory;

        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        BindingOperations.EnableCollectionSynchronization(Tasks, new object());

        AddTaskCommand = new RelayCommand(_ => AddTask());
        DeleteTaskCommand = new RelayCommand(_ => DeleteTaskAsync(), _ => SelectedTask != null && !IsBusy);
        RunTaskCommand = new RelayCommand(_ => RunTaskAsync(), _ => SelectedTask != null && !IsBusy && !IsBackupInProgress);
        DecryptBackupCommand = new RelayCommand(_ => OpenDecryptTab(), _ => SelectedTask != null && !IsBusy && !IsBackupInProgress);
        StartSchedulerCommand = new RelayCommand(_ => StartScheduler(), _ => !IsSchedulerRunning);
        StopSchedulerCommand = new RelayCommand(_ => StopScheduler(), _ => IsSchedulerRunning);
        SaveTaskCommand = new RelayCommand(async _ => await SaveTaskAsync(), _ => CurrentTaskViewModel != null && !IsBusy);
        CancelTaskCommand = new RelayCommand(_ => CancelTask());

        _schedulerService.StatusChanged += (_, e) =>
            ExecuteOnUi(() => UpdateSchedulerState(e.IsRunning, e.IsPrecisionTimerActive, e.Message, true));

        _schedulerService.TaskStarted += (_, e) =>
Notify($"Планировщик: запускаю '{e.Task.Name}' (в {e.ScheduledTime:HH:mm}).");

        _schedulerService.TaskCompleted += async (_, e) =>
        {
            await ExecuteOnUiAsync(async () =>
            {
                Notify(e.IsSuccess
                    ? $"Планировщик: '{e.Task.Name}' выполнена."
                    : $"Планировщик: ошибка при выполнении '{e.Task.Name}': {e.Error}");

                if (e.IsSuccess && e.Result != null)
                {
                    LogBackupSummary("Планировщик", e.Task, e.Result);
                    
                    // Обновляем задачи и историю после выполнения задачи через планировщик
                    await ReloadTasksAsync();
                    await LoadHistoryForSelectedTaskAsync();
                }
            });
        };
}

    // ---------------- Initialization ---------------- //

    public Task InitializeAsync() => ExecuteBusyAsync(async () =>
    {
        await ReloadTasksAsync();
        StartScheduler();
        
        // Инициализируем ViewModels для вкладок
        CurrentDecryptViewModel = _decryptWindowViewModelFactory();
        CurrentDecryptViewModel.CloseRequested += () =>
        {
            SelectedTabIndex = 0; // Возвращаемся на главную вкладку
        };
    });

    // ---------------- Helpers ---------------- //

    private void RefreshCommands()
    {
        AddTaskCommand.RaiseCanExecuteChanged();
        DeleteTaskCommand.RaiseCanExecuteChanged();
        RunTaskCommand.RaiseCanExecuteChanged();
        DecryptBackupCommand.RaiseCanExecuteChanged();
        StartSchedulerCommand.RaiseCanExecuteChanged();
        StopSchedulerCommand.RaiseCanExecuteChanged();
        SaveTaskCommand.RaiseCanExecuteChanged();
    }

    private void ExecuteOnUi(Action a)
    {
        if (_dispatcher.CheckAccess()) a(); else _dispatcher.Invoke(a);
    }

    private async Task ExecuteOnUiAsync(Func<Task> a)
    {
        if (_dispatcher.CheckAccess())
        {
            await a();
        }
        else
        {
            await _dispatcher.InvokeAsync(a);
        }
    }

   private void Notify(string msg, bool log = true)
    {
       StatusMessage = msg;
        OnPropertyChanged(nameof(StatusMessage));

        if (!log) return;

        ExecuteOnUi(() =>
        {
            ExecutionLog.Insert(0, $"{DateTime.Now:HH:mm:ss}  {msg}");
            if (ExecutionLog.Count > AppConstants.MaxExecutionLogEntries)
                ExecutionLog.RemoveAt(ExecutionLog.Count - 1);
        });
    }

    private BackupTask? RequireTask()
    {
        if (SelectedTask == null)
        {
            Notify("Задача не выбрана.");
return null;
        }
       return SelectedTask;
    }

   private async Task WithTaskAsync(Func<BackupTask, Task> action)
    {
        var selected = RequireTask();
        if (selected == null) return;

        var fresh = await _repository.GetByIdAsync(selected.Id, CancellationToken.None);
        if (fresh == null)
        {
            Notify("Задача не найдена.");
            return;
        }

        await action(fresh);
    }

    // ---------------- Task operations ---------------- //

    private async Task ReloadTasksAsync()
    {
        Notify("Загрузка задач...", false);
        
        var allTasks = await _repository.GetAllAsync(CancellationToken.None);
        var taskIds = allTasks.Select(t => t.Id).ToHashSet();

        // Удаляем задачи, которых больше нет
        var toRemove = Tasks.Where(t => !taskIds.Contains(t.Id)).ToList();
        foreach (var task in toRemove)
        {
            Tasks.Remove(task);
        }

        // Обновляем существующие и добавляем новые
        foreach (var task in allTasks)
        {
            var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing != null)
            {
                // Обновляем свойства существующей задачи
                var index = Tasks.IndexOf(existing);
                Tasks[index] = task;
            }
            else
            {
                Tasks.Add(task);
            }
        }

        // Сохраняем выбранную задачу, если она все еще существует
        if (SelectedTask != null)
        {
            var updatedTask = Tasks.FirstOrDefault(t => t.Id == SelectedTask.Id);
            if (updatedTask != null)
            {
                SelectedTask = updatedTask;
            }
            else
            {
                SelectedTask = Tasks.FirstOrDefault();
            }
        }
        else
        {
        SelectedTask = Tasks.FirstOrDefault();
        }

        Notify($"Загружено задач: {Tasks.Count}.", false);
    }

    private async Task LoadHistoryForSelectedTaskAsync()
    {
        SelectedTaskHistory.Clear();
        if (SelectedTask == null)
        {
            return;
        }

        var history = await _repository.GetHistoryForTaskAsync(SelectedTask.Id, CancellationToken.None);
        foreach (var item in history)
        {
            SelectedTaskHistory.Add(item);
        }
    }

    private void AddTask()
    {
        _isEditingTask = false;
        _isCreatingNewTask = true; // Устанавливаем флаг создания новой задачи
        SelectedTask = null; // Очищаем выбранную задачу при создании новой
        CurrentTaskViewModel = _taskVmFactory();
        OnPropertyChanged(nameof(TaskTabHeader));
        SelectedTabIndex = 1; // Переключаемся на вкладку "Задача"
        _isCreatingNewTask = false; // Сбрасываем флаг после переключения
    }

    private void SyncSelectedTaskToViewModel()
    {
        if (SelectedTask == null)
        {
            // Если задача не выбрана, создаем новую
            _isEditingTask = false;
            _isCreatingNewTask = false;
            CurrentTaskViewModel = _taskVmFactory();
            OnPropertyChanged(nameof(TaskTabHeader));
            return;
        }

        _isEditingTask = true;
        _isCreatingNewTask = false;
        if (CurrentTaskViewModel == null)
        {
            CurrentTaskViewModel = _taskVmFactory();
        }
        
        // Загружаем актуальные данные задачи из репозитория асинхронно
        _ = Task.Run(async () =>
        {
            var freshTask = await _repository.GetByIdAsync(SelectedTask.Id, CancellationToken.None);
            if (freshTask != null)
            {
                ExecuteOnUi(() =>
                {
                    CurrentTaskViewModel?.LoadFromTask(freshTask);
                    OnPropertyChanged(nameof(TaskTabHeader));
                });
            }
        });
        
        // Сразу загружаем из выбранной задачи для быстрого отображения
        CurrentTaskViewModel.LoadFromTask(SelectedTask);
        OnPropertyChanged(nameof(TaskTabHeader));
    }

    private async Task SaveTaskAsync()
    {
        if (CurrentTaskViewModel == null) return;

        if (!CurrentTaskViewModel.Validate())
        {
            Notify(CurrentTaskViewModel.ValidationError ?? "Ошибка валидации.");
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            var taskName = CurrentTaskViewModel.Name;
            var excludeId = _isEditingTask && SelectedTask != null ? SelectedTask.Id : (int?)null;

            // Проверяем уникальность имени задачи
            if (!await _repository.IsTaskNameUniqueAsync(taskName, excludeId, CancellationToken.None))
            {
                Notify($"Задача с именем '{taskName}' уже существует. Выберите другое имя.");
                return;
            }

            if (_isEditingTask && SelectedTask != null)
            {
                // Редактирование существующей задачи
                CurrentTaskViewModel.ApplyToTask(SelectedTask);
                await _repository.UpdateAsync(SelectedTask, CancellationToken.None);
                Notify($"Задача '{SelectedTask.Name}' обновлена.");
            }
            else
            {
                // Создание новой задачи
                var task = new BackupTask { CreatedDate = DateTime.UtcNow };
                CurrentTaskViewModel.ApplyToTask(task);
                await _repository.AddAsync(task, CancellationToken.None);
                Tasks.Add(task);
                SelectedTask = task;
                Notify($"Задача '{task.Name}' добавлена.");
            }

            await ReloadTasksAsync();
            SelectedTabIndex = 0; // Возвращаемся на главную вкладку
            CurrentTaskViewModel = null;
        });
    }

    private void CancelTask()
    {
        CurrentTaskViewModel = null;
        SelectedTabIndex = 0; // Возвращаемся на главную вкладку
    }

    private void OpenDecryptTab()
    {
        if (CurrentDecryptViewModel == null)
        {
            CurrentDecryptViewModel = _decryptWindowViewModelFactory();
            CurrentDecryptViewModel.CloseRequested += () =>
            {
                SelectedTabIndex = 0; // Возвращаемся на главную вкладку
            };
        }
        SelectedTabIndex = 2; // Переключаемся на вкладку "Расшифровка"
    }

    private async Task DeleteTaskAsync() =>
        await WithTaskAsync(async task =>
        {
            if (!_messageService.ShowConfirmation(
                $"Удалить задачу '{task.Name}'?", "Подтверждение"))
               return;

            await ExecuteBusyAsync(async () =>
            {
                await _repository.DeleteAsync(task.Id, CancellationToken.None);
                
                // Находим и удаляем задачу из коллекции по Id
                var taskToRemove = Tasks.FirstOrDefault(t => t.Id == task.Id);
                if (taskToRemove != null)
                {
                    Tasks.Remove(taskToRemove);
                }
                
                SelectedTask = Tasks.FirstOrDefault();
                Notify($"Задача '{task.Name}' удалена.");
            });
        });

    // ---------------- Backup ---------------- //

    private void ResetBackupProgress(string name)
    {
       BackupProgress = 0;
       CurrentTransferFile = $"Подготовка '{name}'...";
TransferSpeedText = "—";
        RemainingTimeText = "—";

        OnPropertyChanged(nameof(CurrentTransferFile));
        OnPropertyChanged(nameof(TransferSpeedText));
        OnPropertyChanged(nameof(RemainingTimeText));
}

    private void UpdateBackupProgress(BackupProgressReport r)
   {
        BackupProgress = r.Percentage;
       CurrentTransferFile = string.IsNullOrWhiteSpace(r.CurrentFile) ? "Подготовка..." : r.CurrentFile;
        TransferSpeedText = r.BytesPerSecond > 0 ? $"{FormatHelper.FormatSize(r.BytesPerSecond)}/с" : "—";
        RemainingTimeText = r.EstimatedRemaining == null ? "—" : FormatHelper.FormatDuration(r.EstimatedRemaining.Value);

        OnPropertyChanged(nameof(CurrentTransferFile));
        OnPropertyChanged(nameof(TransferSpeedText));
        OnPropertyChanged(nameof(RemainingTimeText));
    }

    private async Task RunTaskAsync()
    {
        if (IsBackupInProgress || IsBusy)
        {
            Notify("Задача уже выполняется. Пожалуйста, подождите.");
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            var selected = RequireTask();
            if (selected == null) return;

            var taskId = selected.Id;
            ResetBackupProgress(selected.Name);
            IsBackupInProgress = true;

            try
            {
                Notify($"Выполнение '{selected.Name}'...");
                var progress = new Progress<BackupProgressReport>(UpdateBackupProgress);

                // Получаем свежую задачу из БД
                var freshTask = await _repository.GetByIdAsync(taskId, CancellationToken.None);
                if (freshTask == null)
                {
                    Notify("Задача не найдена.");
                    return;
                }

                var result = await _backupService.ExecuteBackupAsync(freshTask, progress, CancellationToken.None);

                var moment = DateTime.UtcNow;
                var lastFull = result.PerformedBackupType == BackupType.Full ? moment : (DateTime?)null;

                await _repository.UpdateLastRunAsync(taskId, moment, lastFull, CancellationToken.None);
                await _repository.AddHistoryAsync(new BackupHistory
                {
                    TaskId = taskId,
                    StartTime = result.StartedAt,
                    EndTime = result.CompletedAt,
                    Status = BackupStatus.Success,
                    BackupType = result.PerformedBackupType,
                    UsedCompression = result.UsedCompression,
                    CompressionLevel = result.CompressionLevel,
                    FilesCopied = result.FilesCopied,
                    TotalSize = result.TotalBytes,
                    OutputArtifactPath = result.OutputArtifactPath,
                    CompressedSize = result.BytesWritten,
                    Duration = result.Duration
                }, CancellationToken.None);

                // Применяем политику хранения
                await _retentionService.ApplyRetentionPolicyAsync(taskId, CancellationToken.None);

                // Обновляем задачи и историю
                await ReloadTasksAsync();
                await LoadHistoryForSelectedTaskAsync();

                // Получаем обновленную задачу для расчета следующего запуска
                var updatedTask = await _repository.GetByIdAsync(taskId, CancellationToken.None);
                if (updatedTask != null)
                {
                    var next = _schedulerService.CalculateNextRun(updatedTask);
                    Notify(next == null
                        ? $"Задача '{updatedTask.Name}' выполнена."
                        : $"Задача '{updatedTask.Name}' выполнена. Следующий запуск: {next.Value:dd.MM.yyyy HH:mm}.");

                    LogBackupSummary("Ручной запуск", updatedTask, result);
                }
            }
            catch (Exception ex)
            {
                var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex);
                Notify($"Ошибка при выполнении задачи: {userMessage}");
                _messageService.ShowMessage(userMessage, "Ошибка выполнения задачи", MessageType.Error);
            }
            finally
            {
                IsBackupInProgress = false;
            }
        });
    }


   // ---------------- Scheduler ---------------- //

   private void StartScheduler()
    {
        try
        {
            _schedulerService.StartScheduler();
            UpdateSchedulerState(true, _schedulerService.IsPrecisionTimerActive, "Планировщик запущен.", true);
        }
        catch (Exception ex)
        {
            Notify($"Не удалось запустить планировщик: {ex.Message}");
        }
    }

    private void StopScheduler()
    {
        try
        {
            _schedulerService.StopScheduler();
            UpdateSchedulerState(false, false, "Планировщик остановлен.", true);
}
        catch (Exception ex)
       {
            Notify($"Не удалось остановить планировщик: {ex.Message}");
        }
    }

    private void UpdateSchedulerState(bool running, bool precision, string msg, bool log)
    {
        IsSchedulerRunning = running;
        IsPrecisionTimerActive = precision;

        SchedulerStatus = msg;
        PrecisionTimerStatus = precision ? "Точный таймер: вкл." : "Точный таймер: выкл.";

        OnPropertyChanged(nameof(SchedulerStatus));
        OnPropertyChanged(nameof(PrecisionTimerStatus));
        RefreshCommands();

        if (log) Notify(msg);
    }

   // ---------------- Logs ---------------- //

    private void LogBackupSummary(string origin, BackupTask task, BackupExecutionResult r)
    {
        var summary =
            $"{origin}: '{task.Name}' — тип: {r.PerformedBackupType}, файлов: {r.FilesCopied}, папок: {r.DirectoriesCreated}, объём: {FormatHelper.FormatSize(r.TotalBytes)}, время: {FormatHelper.FormatDuration(r.Duration)}.";

        if (!string.IsNullOrWhiteSpace(r.OutputArtifactPath))
            summary += $" Артефакт: {r.OutputArtifactPath}.";

        Notify(summary);
    }


    // ---------------- ExecuteBusy ---------------- //

    private async Task ExecuteBusyAsync(Func<Task> action)
{
        if (IsBusy) return;

       try
        {
           IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            var userMessage = ExceptionHelper.GetUserFriendlyMessage(ex);
            Notify($"Ошибка: {userMessage}");
            _messageService.ShowMessage(userMessage, "Ошибка", MessageType.Error);
        }
        finally
       {
            IsBusy = false;
        }
    }
}