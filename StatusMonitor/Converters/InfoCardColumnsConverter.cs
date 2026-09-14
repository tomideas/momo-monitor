using System;
using System.Globalization;
using System.Windows.Data;

namespace StatusMonitor.Converters
{
    /// <summary>
    /// Maps the Info panel's available width to a column count for the
    /// <see cref="Controls.RowUniformPanel"/>. Keeps at least
    /// <c>MinCardWidth</c> per card (plus the gap) and caps at <c>MaxCols</c>.
    /// </summary>
    public sealed class InfoCardColumnsConverter : IValueConverter
    {
        private const double Gap = 16;
        private const double MinCardWidth = 250;
        private const int MaxCols = 4;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not double w || w <= 0)
                return 1;

            double minWidth = (parameter as string) switch { "dashboard" => 300, "compact" => 220, _ => MinCardWidth };
            int cols = (int)Math.Floor((w + Gap) / (minWidth + Gap));
            return Math.Clamp(cols, 1, MaxCols);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

