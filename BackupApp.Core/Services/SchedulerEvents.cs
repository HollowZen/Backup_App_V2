using BackupApp.Core.Models;

namespace BackupApp.Core.Services;

public enum SchedulerTickType
{
    Main,
    Precision
}

public sealed class SchedulerStatusChangedEventArgs : EventArgs
{
    public SchedulerStatusChangedEventArgs(string message, bool isRunning, bool isPrecisionTimerActive, SchedulerTickType tickType)
    {
        Message = message;
        IsRunning = isRunning;
        IsPrecisionTimerActive = isPrecisionTimerActive;
        TickType = tickType;
    }

    public string Message { get; }

    public bool IsRunning { get; }

    public bool IsPrecisionTimerActive { get; }

    public SchedulerTickType TickType { get; }
}

public sealed class SchedulerTaskEventArgs : EventArgs
{
    public SchedulerTaskEventArgs(BackupTask task, DateTime scheduledTime, bool isSuccess, string? error = null, BackupExecutionResult? result = null)
    {
        Task = task;
        ScheduledTime = scheduledTime;
        IsSuccess = isSuccess;
        Error = error;
        Result = result;
    }

    public BackupTask Task { get; }

    public DateTime ScheduledTime { get; }

    public bool IsSuccess { get; }

    public string? Error { get; }

    public BackupExecutionResult? Result { get; }
}

