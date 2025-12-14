using BackupApp.Core.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace BackupApp.WPF.Converters;

public class ScheduleTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScheduleType scheduleType)
        {
            return scheduleType switch
            {
                ScheduleType.Manual => "Вручную",
                ScheduleType.Daily => "Ежедневно",
                ScheduleType.Weekly => "Еженедельно",
                ScheduleType.Monthly => "Ежемесячно",
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
                "Вручную" => ScheduleType.Manual,
                "Ежедневно" => ScheduleType.Daily,
                "Еженедельно" => ScheduleType.Weekly,
                "Ежемесячно" => ScheduleType.Monthly,
                _ => ScheduleType.Manual
            };
        }
        return ScheduleType.Manual;
    }
}




