using BackupApp.Core.Models;

namespace BackupApp.Core.Services;

/// <summary>
/// Интерфейс сервиса шифрования для защиты бэкапов
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Шифрует файл AES с использованием пароля
    /// </summary>
    Task EncryptFileAsync(string sourcePath, string targetPath, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Расшифровывает файл AES с использованием пароля
    /// </summary>
    Task DecryptFileAsync(string sourcePath, string targetPath, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Расшифровывает массив байтов
    /// </summary>
    Task<byte[]> DecryptDataAsync(byte[] encryptedData, string password);

    /// <summary>
    /// Шифрует массив байтов
    /// </summary>
    Task<byte[]> EncryptDataAsync(byte[] data, string password);

    /// <summary>
    /// Создает хэш пароля с солью для безопасного хранения
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Проверяет соответствие пароля хэшу
    /// </summary>
    bool VerifyPassword(string password, string hash);
}
