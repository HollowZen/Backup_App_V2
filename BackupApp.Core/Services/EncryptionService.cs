using System.Security.Cryptography;
using System.Text;

namespace BackupApp.Core.Services;

/// <summary>
/// Реализация сервиса шифрования с использованием AES-256 и PBKDF2
/// </summary>
public class EncryptionService : IEncryptionService
{
    /// <summary>
    /// Размер ключа AES (256 бит)
    /// </summary>
    private const int AesKeySize = 256;

    /// <summary>
    /// Размер блока AES (128 бит)
    /// </summary>
    private const int AesBlockSize = 128;

    /// <summary>
    /// Размер соли (256 бит)
    /// </summary>
    private const int SaltSize = 32;

    /// <summary>
    /// Количество итераций PBKDF2
    /// </summary>
    private const int HashIterations = 10000;

    /// <summary>
    /// Шифрует файл с использованием AES-CBC
    /// </summary>
    /// <param name="sourcePath">Путь к исходному файлу</param>
    /// <param name="targetPath">Путь к зашифрованному файлу</param>
    /// <param name="password">Пароль для шифрования</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task EncryptFileAsync(string sourcePath, string targetPath, string password, CancellationToken cancellationToken = default)
    {
        using var aes = Aes.Create();
        aes.KeySize = AesKeySize;
        aes.BlockSize = AesBlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // генерируем случайную соль и делаем ключ из пароля через PBKDF2
        var salt = GenerateSalt();
        var key = DeriveKey(password, salt);

        aes.Key = key;
        aes.GenerateIV();

        // сохраняем IV и соль в начале файла, они нужны для расшифровки
        var ivAndSalt = new byte[aes.IV.Length + salt.Length];
        Array.Copy(aes.IV, 0, ivAndSalt, 0, aes.IV.Length);
        Array.Copy(salt, 0, ivAndSalt, aes.IV.Length, salt.Length);

        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var cryptoStream = new CryptoStream(targetStream, aes.CreateEncryptor(), CryptoStreamMode.Write);

        // сначала пишем IV и соль
        await targetStream.WriteAsync(ivAndSalt, cancellationToken);

        // потом шифруем и пишем сам файл
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await cryptoStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        await cryptoStream.FlushFinalBlockAsync(cancellationToken);
    }

    /// <summary>
    /// Расшифровывает файл с использованием AES-CBC
    /// </summary>
    /// <param name="sourcePath">Путь к зашифрованному файлу</param>
    /// <param name="targetPath">Путь к расшифрованному файлу</param>
    /// <param name="password">Пароль для расшифровки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task DecryptFileAsync(string sourcePath, string targetPath, string password, CancellationToken cancellationToken = default)
    {
        using var aes = Aes.Create();
        aes.KeySize = AesKeySize;
        aes.BlockSize = AesBlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // читаем IV и соль из начала файла, они там должны быть
        var ivAndSalt = new byte[aes.IV.Length + SaltSize];
        var bytesRead = await sourceStream.ReadAsync(ivAndSalt, cancellationToken);
        if (bytesRead != ivAndSalt.Length)
        {
            throw new InvalidDataException("Неверный формат зашифрованного файла.");
        }

        var iv = new byte[aes.IV.Length];
        var salt = new byte[SaltSize];
        Array.Copy(ivAndSalt, 0, iv, 0, iv.Length);
        Array.Copy(ivAndSalt, iv.Length, salt, 0, salt.Length);

        // делаем ключ из пароля используя соль
        var key = DeriveKey(password, salt);
        aes.Key = key;
        aes.IV = iv;

        await using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var cryptoStream = new CryptoStream(targetStream, aes.CreateDecryptor(), CryptoStreamMode.Write);

        // расшифровываем файл и пишем результат
        var buffer = new byte[8192];
        while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await cryptoStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        await cryptoStream.FlushFinalBlockAsync(cancellationToken);
    }

    public Task<byte[]> EncryptDataAsync(byte[] data, string password)
    {
        using var aes = Aes.Create();
        aes.KeySize = AesKeySize;
        aes.BlockSize = AesBlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var salt = GenerateSalt();
        var key = DeriveKey(password, salt);
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encryptedData = encryptor.TransformFinalBlock(data, 0, data.Length);

        var result = new byte[aes.IV.Length + salt.Length + encryptedData.Length];
        Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
        Array.Copy(salt, 0, result, aes.IV.Length, salt.Length);
        Array.Copy(encryptedData, 0, result, aes.IV.Length + salt.Length, encryptedData.Length);

        return Task.FromResult(result);
    }

    public Task<byte[]> DecryptDataAsync(byte[] encryptedDataWithIv, string password)
    {
        if (encryptedDataWithIv.Length < 16 + SaltSize)
        {
            throw new ArgumentException("Invalid encrypted data format.");
        }

        using var aes = Aes.Create();
        aes.KeySize = AesKeySize;
        aes.BlockSize = AesBlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var iv = new byte[16];
        var salt = new byte[SaltSize];
        Array.Copy(encryptedDataWithIv, 0, iv, 0, 16);
        Array.Copy(encryptedDataWithIv, 16, salt, 0, SaltSize);

        var key = DeriveKey(password, salt);
        aes.Key = key;
        aes.IV = iv;

        var encryptedData = new byte[encryptedDataWithIv.Length - 16 - SaltSize];
        Array.Copy(encryptedDataWithIv, 16 + SaltSize, encryptedData, 0, encryptedData.Length);

        using var decryptor = aes.CreateDecryptor();
        var decryptedData = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);

        return Task.FromResult(decryptedData);
    }

    public string HashPassword(string password)
    {
        var salt = GenerateSalt();
        var hash = DeriveKey(password, salt);

        var hashWithSalt = new byte[salt.Length + hash.Length];
        Array.Copy(salt, 0, hashWithSalt, 0, salt.Length);
        Array.Copy(hash, 0, hashWithSalt, salt.Length, hash.Length);

        return Convert.ToBase64String(hashWithSalt);
    }

    public bool VerifyPassword(string password, string hashString)
    {
        try
        {
            var hashWithSalt = Convert.FromBase64String(hashString);
            if (hashWithSalt.Length != SaltSize + 32) // 32 = 256-bit hash
            {
                return false;
            }

            var storedSalt = new byte[SaltSize];
            var storedHash = new byte[32];
            Array.Copy(hashWithSalt, 0, storedSalt, 0, SaltSize);
            Array.Copy(hashWithSalt, SaltSize, storedHash, 0, 32);

            var computedHash = DeriveKey(password, storedSalt);
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] GenerateSalt()
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, HashIterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key
    }
}
