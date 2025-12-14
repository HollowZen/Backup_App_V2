using BackupApp.Core.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace BackupApp.WPF.Converters;

public class BackupTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BackupType backupType)
        {
            return backupType switch
            {
                BackupType.Full => "Полная",
                BackupType.Incremental => "Инкрементная",
                BackupType.Differential => "Дифференциальная",
                _ => value.ToString()
            };
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "Полная" => BackupType.Full,
                "Инкрементная" => BackupType.Incremental,
                "Дифференциальная" => BackupType.Differential,
                _ => BackupType.Full
            };
        }
        return BackupType.Full;
    }
}




