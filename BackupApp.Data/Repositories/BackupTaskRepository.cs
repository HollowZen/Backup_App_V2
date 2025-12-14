using System;
using BackupApp.Core.Models;
using BackupApp.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BackupApp.Data.Repositories;

public class BackupTaskRepository : IBackupTaskRepository
{
    private readonly BackupAppDbContext _dbContext;

    public BackupTaskRepository(BackupAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<BackupTask>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BackupTasks
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<BackupTask?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BackupTasks
            .Include(t => t.History)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<BackupTask> AddAsync(BackupTask task, CancellationToken cancellationToken = default)
    {
        await _dbContext.BackupTasks.AddAsync(task, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.Entry(task).State = EntityState.Detached;
        return task;
    }

    public async Task UpdateAsync(BackupTask task, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.BackupTasks
            .FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException($"Задача с Id={task.Id} не найдена.");
        }

        _dbContext.Entry(existing).CurrentValues.SetValues(task);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.Entry(existing).State = EntityState.Detached;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.BackupTasks.FindAsync(new object[] { id }, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _dbContext.BackupTasks.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.Entry(existing).State = EntityState.Detached;
    }

    public async Task UpdateLastRunAsync(int taskId, DateTime lastBackupTimeUtc, DateTime? lastFullBackupTimeUtc, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.BackupTasks
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException($"Задача с Id={taskId} не найдена.");
        }

        existing.LastBackupTime = lastBackupTimeUtc;
        if (lastFullBackupTimeUtc.HasValue)
        {
            existing.LastFullBackupTime = lastFullBackupTimeUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.Entry(existing).State = EntityState.Detached;
    }

    public async Task DeleteHistoryAsync(int historyId, CancellationToken cancellationToken = default)
    {
        var history = await _dbContext.BackupHistories.FindAsync(new object[] { historyId }, cancellationToken);
        if (history is null)
        {
            return;
        }

        _dbContext.BackupHistories.Remove(history);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}










