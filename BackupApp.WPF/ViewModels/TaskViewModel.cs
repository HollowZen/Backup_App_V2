using BackupApp.Core.Constants;
using BackupApp.Core.Models;
using BackupApp.Core.Services;
using BackupApp.WPF.Commands;
using BackupApp.WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BackupApp.WPF.ViewModels;

public class TaskViewModel : ObservableObject
{
    private readonly IPathDialogService _pathDialogService;
    private readonly IEncryptionService _encryptionService;

    private int _id;
    private string _name = string.Empty;
    private string _sourcePath = string.Empty;
    private string _targetPath = string.Empty;
    private ScheduleType _scheduleType = ScheduleType.Manual;
    private string? _scheduleTime;
    private string? _scheduleDays;
    private int? _scheduleDayOfMonth;
    private bool _isEnabled = true;
    private string? _validationError;
    private bool _suppressWeeklyDayUpdates;
    private BackupType _backupType = BackupType.Full;
    private bool _useCompression;
    private int _compressionLevel = AppConstants.DefaultCompressionLevel;
    private bool _useEncryption;
    private string? _encryptionPassword;
    private RetentionPolicyType _retentionPolicyType = RetentionPolicyType.KeepAll;
    private int? _maxVersions;
    private int? _maxAgeDays;

    public IReadOnlyList<ScheduleType> ScheduleTypes { get; } = Enum.GetValues<ScheduleType>();
    public IReadOnlyList<BackupType> BackupTypes { get; } = Enum.GetValues<BackupType>();
    public IReadOnlyList<RetentionPolicyType> RetentionPolicyTypes { get; } = Enum.GetValues<RetentionPolicyType>();
    public IReadOnlyList<int> MonthDayOptions { get; } = Enumerable.Range(AppConstants.MinMonthDay, AppConstants.MaxMonthDay).ToArray();
    public ObservableCollection<WeekDayOption> WeeklyDayOptions { get; }

    public RelayCommand BrowseSourcePathCommand { get; }
    public RelayCommand BrowseTargetPathCommand { get; }

    public TaskViewModel(IPathDialogService pathDialogService, IEncryptionService encryptionService)
    {
        _pathDialogService = pathDialogService;
        _encryptionService = encryptionService;
        BrowseSourcePathCommand = new RelayCommand(_ => BrowseSourcePath());
        BrowseTargetPathCommand = new RelayCommand(_ => BrowseTargetPath());
        WeeklyDayOptions = new ObservableCollection<WeekDayOption>(CreateWeekDayOptions());
        foreach (var option in WeeklyDayOptions)
        {
            option.PropertyChanged += OnWeekDayOptionChanged;
        }
    }

    public int Id
    {
        get => _id;
        private set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value);
    }

    public ScheduleType ScheduleType
    {
        get => _scheduleType;
        set => SetProperty(ref _scheduleType, value);
    }

    public string? ScheduleTime
    {
        get => _scheduleTime;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SetProperty(ref _scheduleTime, null);
                return;
            }

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time))
            {
                value = time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            }

            if (SetProperty(ref _scheduleTime, value))
            {
                OnPropertyChanged(nameof(ScheduleTimeValue));
            }
        }
    }

    public TimeSpan? ScheduleTimeValue
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ScheduleTime))
            {
                return null;
            }

            return TimeSpan.TryParse(ScheduleTime, CultureInfo.InvariantCulture, out var time)
                ? time
                : null;
        }
        set => ScheduleTime = value?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    public string? ScheduleDays
    {
        get => _scheduleDays;
        private set => SetProperty(ref _scheduleDays, value);
    }

    public int? ScheduleDayOfMonth
    {
        get => _scheduleDayOfMonth;
        set => SetProperty(ref _scheduleDayOfMonth, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string? ValidationError
    {
        get => _validationError;
        private set => SetProperty(ref _validationError, value);
    }

    public BackupType BackupType
    {
        get => _backupType;
        set => SetProperty(ref _backupType, value);
    }

    public bool UseCompression
    {
        get => _useCompression;
        set => SetProperty(ref _useCompression, value);
    }

    public int CompressionLevel
    {
        get => _compressionLevel;
        set => SetProperty(ref _compressionLevel, Math.Clamp(value, AppConstants.MinCompressionLevel, AppConstants.MaxCompressionLevel));
    }

    public bool UseEncryption
    {
        get => _useEncryption;
        set => SetProperty(ref _useEncryption, value);
    }

    public string? EncryptionPassword
    {
        get => _encryptionPassword;
        set => SetProperty(ref _encryptionPassword, value);
    }

    public void SetEncryptionPassword(string password)
    {
        _encryptionPassword = password;
        OnPropertyChanged(nameof(EncryptionPassword));
    }

    public RetentionPolicyType RetentionPolicyType
    {
        get => _retentionPolicyType;
        set => SetProperty(ref _retentionPolicyType, value);
    }

    public int? MaxVersions
    {
        get => _maxVersions;
        set => SetProperty(ref _maxVersions, value);
    }

    public int? MaxAgeDays
    {
        get => _maxAgeDays;
        set => SetProperty(ref _maxAgeDays, value);
    }

    private void BrowseSourcePath()
    {
        var path = _pathDialogService.BrowseFolder(SourcePath, "Выберите папку источника");
        if (!string.IsNullOrWhiteSpace(path))
        {
            SourcePath = path;
        }
    }

    private void BrowseTargetPath()
    {
        var path = _pathDialogService.BrowseFolder(TargetPath, "Выберите папку назначения");
        if (!string.IsNullOrWhiteSpace(path))
        {
            TargetPath = path;
        }
    }

    public void LoadFromTask(BackupTask task)
    {
        Id = task.Id;
        Name = task.Name;
        SourcePath = task.SourcePath;
        TargetPath = task.TargetPath;
        ScheduleType = task.ScheduleType;
        ScheduleTime = task.ScheduleTime;
        ScheduleDays = task.ScheduleDays;
        ApplyScheduleDaysToOptions();
        ScheduleDayOfMonth = task.ScheduleDayOfMonth;
        IsEnabled = task.IsEnabled;
        BackupType = task.BackupType;
        UseCompression = task.UseCompression;
        CompressionLevel = task.CompressionLevel;
        UseEncryption = task.UseEncryption;
        // Пароль не загружаем для безопасности
        EncryptionPassword = null;
        RetentionPolicyType = task.RetentionPolicyType;
        MaxVersions = task.MaxVersions;
        MaxAgeDays = task.MaxAgeDays;
    }

    public void ApplyToTask(BackupTask task)
    {
        task.Name = Name.Trim();
        task.SourcePath = SourcePath.Trim();
        task.TargetPath = TargetPath.Trim();
        task.ScheduleType = ScheduleType;
        if (ScheduleType == ScheduleType.Manual)
        {
            task.ScheduleTime = null;
            task.ScheduleDays = null;
            task.ScheduleDayOfMonth = null;
        }
        else
        {
            task.ScheduleTime = ScheduleTime?.Trim();
            task.ScheduleDays = ScheduleDays?.Trim();
            task.ScheduleDayOfMonth = ScheduleDayOfMonth;
        }
        task.IsEnabled = IsEnabled;
        task.BackupType = BackupType;
        task.UseCompression = UseCompression;
        task.CompressionLevel = CompressionLevel;
        task.UseEncryption = UseEncryption;
        task.EncryptionPasswordHash = UseEncryption
            ? BuildProtectedPasswordPayload()
            : null;
        task.RetentionPolicyType = RetentionPolicyType;
        if (RetentionPolicyType == RetentionPolicyType.KeepAll)
        {
            task.MaxVersions = null;
            task.MaxAgeDays = null;
        }
        else if (RetentionPolicyType == RetentionPolicyType.MaxVersions)
        {
            task.MaxVersions = MaxVersions;
            task.MaxAgeDays = null;
        }
        else if (RetentionPolicyType == RetentionPolicyType.MaxAge)
        {
            task.MaxVersions = null;
            task.MaxAgeDays = MaxAgeDays;
        }
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Имя задачи обязательно.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            ValidationError = "Укажите путь источника.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(TargetPath))
        {
            ValidationError = "Укажите путь назначения.";
            return false;
        }

        if (ScheduleType != ScheduleType.Manual)
        {
            if (ScheduleTimeValue is null)
            {
                ValidationError = "Для расписания необходимо указать время (HH:mm).";
                return false;
            }

            if (ScheduleType == ScheduleType.Weekly)
            {
                if (!WeeklyDayOptions.Any(o => o.IsSelected))
                {
                    ValidationError = "Выберите хотя бы один день недели.";
                    return false;
                }
            }

            if (ScheduleType == ScheduleType.Monthly)
            {
                if (ScheduleDayOfMonth is null or < AppConstants.MinMonthDay or > AppConstants.MaxMonthDay)
                {
                    ValidationError = $"День месяца должен быть в диапазоне {AppConstants.MinMonthDay}-{AppConstants.MaxMonthDay}.";
                    return false;
                }
            }
        }

        if (UseCompression && (CompressionLevel < AppConstants.MinCompressionLevel || CompressionLevel > AppConstants.MaxCompressionLevel))
        {
            ValidationError = $"Уровень сжатия должен быть в пределах {AppConstants.MinCompressionLevel}-{AppConstants.MaxCompressionLevel}.";
            return false;
        }

        if (UseEncryption)
        {
            if (string.IsNullOrWhiteSpace(EncryptionPassword))
            {
                ValidationError = "Для шифрования необходимо указать пароль.";
                return false;
            }

            if (EncryptionPassword.Length < AppConstants.MinPasswordLength)
            {
                ValidationError = $"Пароль должен содержать минимум {AppConstants.MinPasswordLength} символов.";
                return false;
            }

            if (EncryptionPassword.Length > AppConstants.MaxPasswordLength)
            {
                ValidationError = $"Пароль не должен превышать {AppConstants.MaxPasswordLength} символов.";
                return false;
            }
        }

        if (RetentionPolicyType == RetentionPolicyType.MaxVersions)
        {
            if (!MaxVersions.HasValue || MaxVersions.Value < 1)
            {
                ValidationError = "Количество версий должно быть больше 0.";
                return false;
            }
        }

        if (RetentionPolicyType == RetentionPolicyType.MaxAge)
        {
            if (!MaxAgeDays.HasValue || MaxAgeDays.Value < 1)
            {
                ValidationError = "Количество дней должно быть больше 0.";
                return false;
            }
        }

        ValidationError = null;
        return true;
    }

    private void OnWeekDayOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WeekDayOption.IsSelected) || _suppressWeeklyDayUpdates)
        {
            return;
        }

        UpdateScheduleDaysFromOptions();
    }

    private void UpdateScheduleDaysFromOptions()
    {
        var selected = WeeklyDayOptions
            .Where(o => o.IsSelected)
            .Select(o => o.Key)
            .ToArray();

        _suppressWeeklyDayUpdates = true;
        ScheduleDays = selected.Length == 0 ? null : string.Join(",", selected);
        _suppressWeeklyDayUpdates = false;
    }

    private void ApplyScheduleDaysToOptions()
    {
        var selected = (ScheduleDays ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeDayKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _suppressWeeklyDayUpdates = true;
        foreach (var option in WeeklyDayOptions)
        {
            option.IsSelected = selected.Contains(option.Key);
        }
        _suppressWeeklyDayUpdates = false;
    }

    private static string NormalizeDayKey(string key)
    {
        return key.ToLowerInvariant() switch
        {
            "monday" or "mon" or "пн" => "Mon",
            "tuesday" or "tue" or "вт" => "Tue",
            "wednesday" or "wed" or "ср" => "Wed",
            "thursday" or "thu" or "чт" => "Thu",
            "friday" or "fri" or "пт" => "Fri",
            "saturday" or "sat" or "сб" => "Sat",
            "sunday" or "sun" or "вс" => "Sun",
            _ => key
        };
    }

    private static WeekDayOption[] CreateWeekDayOptions() =>
        new[]
        {
            new WeekDayOption("Mon", "Пн"),
            new WeekDayOption("Tue", "Вт"),
            new WeekDayOption("Wed", "Ср"),
            new WeekDayOption("Thu", "Чт"),
            new WeekDayOption("Fri", "Пт"),
            new WeekDayOption("Sat", "Сб"),
            new WeekDayOption("Sun", "Вс")
        };

    private string? BuildProtectedPasswordPayload()
    {
        if (!UseEncryption || string.IsNullOrWhiteSpace(EncryptionPassword))
        {
            return null;
        }

        var hash = _encryptionService.HashPassword(EncryptionPassword);
        var entropy = Encoding.UTF8.GetBytes(AppConstants.EncryptionEntropy);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(EncryptionPassword), entropy, DataProtectionScope.CurrentUser);
        var protectedString = Convert.ToBase64String(protectedBytes);
        return $"{hash}|{protectedString}";
    }
}

public class WeekDayOption : ObservableObject
{
    private bool _isSelected;

    public WeekDayOption(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }

    public string Key { get; }
    public string DisplayName { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
