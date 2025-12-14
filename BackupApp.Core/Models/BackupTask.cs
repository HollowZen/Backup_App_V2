namespace BackupApp.Core.Models;

public enum RetentionPolicyType
{
    KeepAll = 0,
    MaxVersions = 1,
    MaxAge = 2
}

public enum ScheduleType
{
    Manual = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

public enum BackupType
{
    Full = 0,
    Incremental = 1,
    Differential = 2
}

public class BackupTask
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public ScheduleType ScheduleType { get; set; }

    /// <summary>
    /// Время запуска (часы:минуты). Можно хранить как TimeSpan, но для простоты - строка "HH:mm".
    /// </summary>
    public string? ScheduleTime { get; set; }

    /// <summary>
    /// Дни недели для расписания (например, "Mon,Tue"). Для еженедельных задач.
    /// </summary>
    public string? ScheduleDays { get; set; }

    /// <summary>
    /// День месяца для ежемесячных задач.
    /// </summary>
    public int? ScheduleDayOfMonth { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public BackupType BackupType { get; set; } = BackupType.Full;

    public bool UseCompression { get; set; }

    /// <summary>
    /// Диапазон 0-9, где 0 — без сжатия, 9 — максимум.
    /// </summary>
    public int CompressionLevel { get; set; } = 5;

    /// <summary>
    /// Время последнего успешного бэкапа (любой тип).
    /// </summary>
    public DateTime? LastBackupTime { get; set; }

    /// <summary>
    /// Время последнего успешного полного бэкапа.
    /// </summary>
    public DateTime? LastFullBackupTime { get; set; }

    /// <summary>
    /// Использовать шифрование
    /// </summary>
    public bool UseEncryption { get; set; }

    /// <summary>
    /// Хеш пароля для шифрования (PBKDF2 + соль)
    /// </summary>
    public string? EncryptionPasswordHash { get; set; }

    /// <summary>
    /// Тип политики хранения
    /// </summary>
    public RetentionPolicyType RetentionPolicyType { get; set; } = RetentionPolicyType.KeepAll;

    /// <summary>
    /// Максимальное количество версий для RetentionPolicyType.MaxVersions
    /// </summary>
    public int? MaxVersions { get; set; }

    /// <summary>
    /// Максимальный возраст в днях для RetentionPolicyType.MaxAge
    /// </summary>
    public int? MaxAgeDays { get; set; }

    public ICollection<BackupHistory> History { get; set; } = new List<BackupHistory>();
}
