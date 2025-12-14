using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BackupApp.WPF.Converters;

public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return Visibility.Collapsed;

        var enumValue = value.ToString();
        var targetEnum = parameter.ToString();

        return enumValue == targetEnum ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}




