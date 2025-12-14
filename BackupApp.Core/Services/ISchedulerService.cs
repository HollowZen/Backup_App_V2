using BackupApp.Core.Models;

namespace BackupApp.Core.Services;

public interface ISchedulerService
{
    event EventHandler<SchedulerStatusChangedEventArgs>? StatusChanged;
    event EventHandler<SchedulerTaskEventArgs>? TaskStarted;
    event EventHandler<SchedulerTaskEventArgs>? TaskCompleted;

    bool IsRunning { get; }
    bool IsPrecisionTimerActive { get; }
    bool IsDebugEnabled { get; }

    void StartScheduler(CancellationToken cancellationToken = default);
    void StopScheduler();
    void SetDebugMode(bool enabled);
    DateTime? CalculateNextRun(BackupTask task, DateTime? from = null);
}





