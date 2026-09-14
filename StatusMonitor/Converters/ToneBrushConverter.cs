using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StatusMonitor.Views;

namespace StatusMonitor.Converters;

/// <summary>
/// Maps a <see cref="BarTone"/> onto the ink / accent / alarm trio so the single-volt rule is
/// expressed once in the view model and never re-stated in XAML.
/// </summary>
public sealed class ToneBrushConverter : IValueConverter
{
    /// <summary>
    /// Numerals stay ink so the bars carry the colour; alarm is the only state that recolours
    /// them, which keeps "coloured number" meaning exactly one thing.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value is BarTone.Alarm ? "AlarmBrush" : "InkBrush";
        return Application.Current?.TryFindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
