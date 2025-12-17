namespace BackupApp.Core.Constants;

/// <summary>
/// Константы приложения
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Энтропия для шифрования паролей через Windows DPAPI
    /// </summary>
    public const string EncryptionEntropy = "BackupAppEncryptionEntropy_v1";

    /// <summary>
    /// Минимальная длина пароля шифрования
    /// </summary>
    public const int MinPasswordLength = 8;

    /// <summary>
    /// Максимальная длина пароля шифрования
    /// </summary>
    public const int MaxPasswordLength = 128;

    /// <summary>
    /// Максимальное количество записей в логе выполнения
    /// </summary>
    public const int MaxExecutionLogEntries = 50;

    /// <summary>
    /// Минимальный день месяца для расписания
    /// </summary>
    public const int MinMonthDay = 1;

    /// <summary>
    /// Максимальный день месяца для расписания
    /// </summary>
    public const int MaxMonthDay = 31;

    /// <summary>
    /// Минимальный уровень сжатия
    /// </summary>
    public const int MinCompressionLevel = 0;

    /// <summary>
    /// Максимальный уровень сжатия
    /// </summary>
    public const int MaxCompressionLevel = 9;

    /// <summary>
    /// Значение по умолчанию для уровня сжатия
    /// </summary>
    public const int DefaultCompressionLevel = 5;
}




