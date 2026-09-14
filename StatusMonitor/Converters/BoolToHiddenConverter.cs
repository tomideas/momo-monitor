using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StatusMonitor.Converters;

/// <summary>
/// True shows, false hides but keeps the space — unlike <see cref="BooleanToVisibilityConverter"/>,
/// which collapses. Used where two alternatives occupy one slot and the block around them must
/// not change height as you switch between them.
/// </summary>
public sealed class BoolToHiddenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Hidden;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
