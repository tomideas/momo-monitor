using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StatusMonitor.Services;
using StatusMonitor.I18n;

namespace StatusMonitor.Views;

public sealed class TrendChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points), typeof(HistoryPoint[]), typeof(TrendChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SecondsProperty = DependencyProperty.Register(nameof(Seconds), typeof(int), typeof(TrendChart), new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CeilingProperty = DependencyProperty.Register(nameof(Ceiling), typeof(double), typeof(TrendChart), new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(TrendChart), new FrameworkPropertyMetadata("%", FrameworkPropertyMetadataOptions.AffectsRender));
    public HistoryPoint[]? Points { get => (HistoryPoint[]?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public int Seconds { get => (int)GetValue(SecondsProperty); set => SetValue(SecondsProperty, value); }
    public double Ceiling { get => (double)GetValue(CeilingProperty); set => SetValue(CeilingProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    private Brush _blue => (Brush)FindResource("AccentBrush");
    private Brush _muted => (Brush)FindResource("MutedBrush");

    private void Text(DrawingContext dc, string text, double x, double y)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 11, _muted, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(x, y));
    }

    protected override void OnRender(DrawingContext dc)
    {
        double left = 46, top = 8, width = ActualWidth - 54, height = ActualHeight - 34;
        if (width <= 0 || height <= 0) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        double max = Math.Max(1, Ceiling);
        var gridPen = new Pen((Brush)FindResource("HairBrush"), 1);
        for (int i = 0; i < 3; i++)
        {
            double y = top + height * i / 2;
            dc.DrawLine(gridPen, new Point(left, y), new Point(left + width, y));
            Text(dc, (max * (2 - i) / 2).ToString("0") + Unit, 0, y - 6);
        }
        Text(dc, Seconds == 900 ? "−15 min" : "−60 s", left, top + height + 6);
        Text(dc, Loc.Instance["now"], left + width - 28, top + height + 6);
        var points = Points;
        if (points is null || !points.Any(p => p.Value.HasValue))
        { Text(dc, Loc.Instance["trend_wait"], left + 12, top + height / 2 - 20); return; }
        DateTimeOffset now = points[^1].Time;
        var pen = new Pen(_blue, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        Point? previous = null;
        DateTimeOffset? lastTime = null;
        foreach (var point in points)
        {
            if (!point.Value.HasValue) { previous = null; lastTime = null; continue; }
            var screen = new Point(left + width * Math.Clamp(1 - (now - point.Time).TotalSeconds / Math.Max(1, Seconds), 0, 1),
                top + height * (1 - Math.Clamp(point.Value.Value / max, 0, 1)));
            if (previous.HasValue && lastTime.HasValue && point.Time - lastTime.Value <= TimeSpan.FromSeconds(5)) dc.DrawLine(pen, previous.Value, screen);
            else dc.DrawEllipse(_blue, null, screen, 2, 2);
            previous = screen;
            lastTime = point.Time;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Points is not { Length: > 0 } points || ActualWidth <= 54) return;
        double fraction = Math.Clamp((e.GetPosition(this).X - 46) / (ActualWidth - 54), 0, 1);
        var time = points[^1].Time.AddSeconds(-(1 - fraction) * Seconds);
        var nearest = points.MinBy(p => Math.Abs((p.Time - time).TotalSeconds));
        ToolTip = nearest is not null && Math.Abs((nearest.Time - time).TotalSeconds) <= 5 && nearest.Value.HasValue
            ? $"{nearest.Time.LocalDateTime:HH:mm:ss}  {nearest.Value:0.0}{Unit}" : Loc.Instance["no_data"];
    }
}

