using BackupApp.Core.Models;

namespace BackupApp.Core.Services;

public interface IBackupService
{
    Task<BackupExecutionResult> ExecuteBackupAsync(BackupTask task, IProgress<BackupProgressReport>? progress = null, CancellationToken cancellationToken = default);
    Task<long> CalculateDirectorySizeAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Расшифровывает зашифрованный архив резервной копии
    /// </summary>
    /// <param name="encryptedFilePath">Путь к зашифрованному файлу</param>
    /// <param name="outputDirectory">Директория для расшифрованных файлов</param>
    /// <param name="password">Пароль для расшифровки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения расшифровки</returns>
    Task<BackupExecutionResult> DecryptBackupAsync(string encryptedFilePath, string outputDirectory, string password, CancellationToken cancellationToken = default);
}