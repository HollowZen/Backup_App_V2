using System;
using System.Linq;
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

    public async Task AddHistoryAsync(BackupHistory history, CancellationToken cancellationToken = default)
    {
        await _dbContext.BackupHistories.AddAsync(history, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.Entry(history).State = EntityState.Detached;
    }

    public async Task<IReadOnlyList<BackupHistory>> GetHistoryForTaskAsync(int taskId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BackupHistories
            .AsNoTracking()
            .Where(h => h.TaskId == taskId)
            .OrderByDescending(h => h.StartTime)
            .ToListAsync(cancellationToken);
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

    public async Task<bool> IsTaskNameUniqueAsync(string name, int? excludeTaskId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Используем ToLower() для регистронезависимого сравнения, так как EF Core не поддерживает
        // string.Equals с StringComparison в SQL запросах
        var nameLower = name.ToLower();
        var query = _dbContext.BackupTasks
            .Where(t => t.Name.ToLower() == nameLower);

        if (excludeTaskId.HasValue)
        {
            query = query.Where(t => t.Id != excludeTaskId.Value);
        }

        var exists = await query.AnyAsync(cancellationToken);
        return !exists;
    }
}










