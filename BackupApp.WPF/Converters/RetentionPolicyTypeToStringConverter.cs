using BackupApp.Core.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace BackupApp.WPF.Converters;

public class RetentionPolicyTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RetentionPolicyType retentionType)
        {
            return retentionType switch
            {
                RetentionPolicyType.KeepAll => "Хранить все",
                RetentionPolicyType.MaxVersions => "Макс. кол-во версий",
                RetentionPolicyType.MaxAge => "Макс. возраст",
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
                "Хранить все" => RetentionPolicyType.KeepAll,
                "Макс. кол-во версий" => RetentionPolicyType.MaxVersions,
                "Макс. возраст" => RetentionPolicyType.MaxAge,
                _ => RetentionPolicyType.KeepAll
            };
        }
        return RetentionPolicyType.KeepAll;
    }
}




