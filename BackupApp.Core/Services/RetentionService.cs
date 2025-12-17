using System.IO;
using BackupApp.Core.Models;
using BackupApp.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace BackupApp.Core.Services;

/// <summary>
/// Сервис управления политиками хранения бэкапов
/// </summary>
public class RetentionService : IRetentionService
{
    private readonly IBackupTaskRepository _repository;
    private readonly ILogger<RetentionService> _logger;

    /// <summary>
    /// Создает новый экземпляр сервиса хранения
    /// </summary>
    /// <param name="repository">Репозиторий задач бэкапа</param>
    /// <param name="logger">Логгер для записи операций</param>
    public RetentionService(IBackupTaskRepository repository, ILogger<RetentionService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task ApplyRetentionPolicyAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var task = await _repository.GetByIdAsync(taskId, cancellationToken);
        if (task is null || task.RetentionPolicyType == RetentionPolicyType.KeepAll)
        {
            return;
        }

        var histories = task.History.OrderByDescending(h => h.StartTime).ToList();
        if (histories.Count == 0)
        {
            return;
        }

        var toDelete = GetVersionsToDelete(histories, task.RetentionPolicyType, task.MaxVersions, task.MaxAgeDays);
        if (toDelete.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Applying retention policy for task {TaskName} ({TaskId}): deleting {Count} old backups", task.Name, taskId, toDelete.Count);

        foreach (var history in toDelete)
        {
            try
            {
                if (!string.IsNullOrEmpty(history.OutputArtifactPath))
                {
                    var path = history.OutputArtifactPath;

                    if (Directory.Exists(path))
                {
                        Directory.Delete(path, recursive: true);
                        _logger.LogInformation("Deleted backup directory: {Path}", path);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                        _logger.LogInformation("Deleted backup file: {Path}", path);
                    }
                }

                await _repository.DeleteHistoryAsync(history.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete backup artifact {Path} for task {TaskId}", history.OutputArtifactPath, taskId);
            }
        }

        _logger.LogInformation("Retention policy applied for task {TaskName}: {DeletedCount} backups deleted, {KeptCount} kept",
            task.Name, toDelete.Count, histories.Count - toDelete.Count);
    }

    public async Task<int> CountVersionsAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var task = await _repository.GetByIdAsync(taskId, cancellationToken);
        return task?.History?.Count ?? 0;
    }

    public ICollection<BackupHistory> GetVersionsToDelete(IReadOnlyList<BackupHistory> histories, RetentionPolicyType policy, int? maxVersions, int? maxAgeDays)
    {
        var result = new List<BackupHistory>();

        switch (policy)
        {
            case RetentionPolicyType.MaxVersions:
                if (maxVersions.HasValue && maxVersions.Value > 0)
                {
                    result.AddRange(histories.Skip(maxVersions.Value));
                }
                break;

            case RetentionPolicyType.MaxAge:
                if (maxAgeDays.HasValue && maxAgeDays.Value > 0)
                {
                    var cutoffDate = DateTime.UtcNow.AddDays(-maxAgeDays.Value);
                    result.AddRange(histories.Where(h => h.StartTime < cutoffDate));
                }
                break;
        }

        return result;
    }
}
