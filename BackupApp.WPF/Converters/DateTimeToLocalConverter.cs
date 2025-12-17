using System;
using System.Globalization;
using System.Windows.Data;

namespace BackupApp.WPF.Converters;

/// <summary>
/// Конвертер для преобразования UTC времени в локальное время
/// </summary>
public class DateTimeToLocalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            // Если время в UTC (Kind == Utc), преобразуем в локальное
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return dateTime.ToLocalTime();
            }
            // Если Kind == Unspecified, предполагаем что это UTC (так как SQLite не сохраняет Kind)
            // и преобразуем в локальное время
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                // Предполагаем, что время из БД хранится в UTC
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).ToLocalTime();
            }
            // Если время уже локальное, возвращаем как есть
            return dateTime;
        }

        // Обработка nullable DateTime через as
        var nullableDt = value as DateTime?;
        if (nullableDt.HasValue)
        {
            var dt = nullableDt.Value;
            if (dt.Kind == DateTimeKind.Utc)
            {
                return dt.ToLocalTime();
            }
            // Если Kind == Unspecified, предполагаем что это UTC
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();
            }
            return dt;
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            // При сохранении преобразуем локальное время в UTC
            if (dateTime.Kind == DateTimeKind.Local || dateTime.Kind == DateTimeKind.Unspecified)
            {
                return dateTime.ToUniversalTime();
            }
            return dateTime;
        }

        return value;
    }
}

