using System;
using BackupApp.Core.Models;

namespace BackupApp.Core.Repositories;

public interface IBackupTaskRepository
{
    Task<IReadOnlyList<BackupTask>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BackupTask?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<BackupTask> AddAsync(BackupTask task, CancellationToken cancellationToken = default);
    Task UpdateAsync(BackupTask task, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateLastRunAsync(int taskId, DateTime lastBackupTimeUtc, DateTime? lastFullBackupTimeUtc, CancellationToken cancellationToken = default);
    Task DeleteHistoryAsync(int historyId, CancellationToken cancellationToken = default);
}




















