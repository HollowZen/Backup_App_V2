using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BackupApp.WPF.Controls;

public partial class TimePickerControl : UserControl
{
    private const int MinutesPerDay = 24 * 60;

    public static readonly DependencyProperty SelectedTimeProperty =
        DependencyProperty.Register(
            nameof(SelectedTime),
            typeof(TimeSpan?),
            typeof(TimePickerControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, CoerceSelectedTime));

    public static readonly DependencyProperty MinuteStepProperty =
        DependencyProperty.Register(
            nameof(MinuteStep),
            typeof(int),
            typeof(TimePickerControl),
            new PropertyMetadata(5, null, CoerceMinuteStep));

    public TimePickerControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Focusable = true;
            FocusVisualStyle = null;
            PreviewKeyDown += OnPreviewKeyDown;
            PreviewMouseWheel += OnPreviewMouseWheel;
        };
    }

    public TimeSpan? SelectedTime
    {
        get => (TimeSpan?)GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    public int MinuteStep
    {
        get => (int)GetValue(MinuteStepProperty);
        set => SetValue(MinuteStepProperty, value);
    }

    private static object? CoerceMinuteStep(DependencyObject _, object? baseValue)
    {
        var value = baseValue as int? ?? 1;
        return Math.Clamp(value, 1, 30);
    }

    private static object? CoerceSelectedTime(DependencyObject _, object? baseValue)
    {
        if (baseValue is not TimeSpan timeSpan)
        {
            return baseValue;
        }

        return new TimeSpan(timeSpan.Hours, timeSpan.Minutes, 0);
    }

    private void OnHourUpClick(object sender, RoutedEventArgs e) => AdjustHours(1);

    private void OnHourDownClick(object sender, RoutedEventArgs e) => AdjustHours(-1);

    private void OnMinuteUpClick(object sender, RoutedEventArgs e) => AdjustMinutes(1);

    private void OnMinuteDownClick(object sender, RoutedEventArgs e) => AdjustMinutes(-1);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                AdjustMinutes(1);
                e.Handled = true;
                break;
            case Key.Down:
                AdjustMinutes(-1);
                e.Handled = true;
                break;
            case Key.Left:
                AdjustHours(-1);
                e.Handled = true;
                break;
            case Key.Right:
                AdjustHours(1);
                e.Handled = true;
                break;
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
        {
            AdjustMinutes(1);
        }
        else
        {
            AdjustMinutes(-1);
        }

        e.Handled = true;
    }

    private void AdjustHours(int delta)
    {
        var current = SelectedTime ?? TimeSpan.Zero;
        var totalMinutes = (current.Hours * 60 + current.Minutes + delta * 60) % MinutesPerDay;
        if (totalMinutes < 0)
        {
            totalMinutes += MinutesPerDay;
        }

        SelectedTime = TimeSpan.FromMinutes(totalMinutes);
    }

    private void AdjustMinutes(int deltaSteps)
    {
        var step = MinuteStep <= 0 ? 1 : MinuteStep;
        var current = SelectedTime ?? TimeSpan.Zero;
        var totalMinutes = current.Hours * 60 + current.Minutes + deltaSteps * step;
        totalMinutes %= MinutesPerDay;
        if (totalMinutes < 0)
        {
            totalMinutes += MinutesPerDay;
        }

        SelectedTime = TimeSpan.FromMinutes(totalMinutes);
    }
}






