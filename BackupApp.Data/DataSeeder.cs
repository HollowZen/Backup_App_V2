using BackupApp.Core.Models;
using BackupApp.Core.Repositories;

namespace BackupApp.Data;

/// <summary>
/// Простейший сидер данных для ручного тестирования репозитория.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IBackupTaskRepository repository, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetAllAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        var task = new BackupTask
        {
            Name = "test",
            SourcePath = "C:\\Temp\\Source",
            TargetPath = "C:\\Temp\\Target",
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "09:33",
            IsEnabled = true,
            CreatedDate = DateTime.UtcNow,
            BackupType = BackupType.Full,
            UseCompression = false,
            CompressionLevel = 5
        };

        await repository.AddAsync(task, cancellationToken);
    }
}




















