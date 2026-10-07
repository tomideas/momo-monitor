using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace StatusMonitor.Views;

/// <summary>
/// What a bar is saying. Every bar is drawn in the skin's accent — the fill lengths share one
/// baseline, so length already does the comparing and colour is free to mean exactly one thing:
/// whether this reading needs attention. Colouring only the highest bar made the colour
/// arbitrary, since being the largest of several small numbers says nothing.
/// </summary>
public enum BarTone
{
    /// <summary>Accent. Every reading below the threshold.</summary>
    Normal,
    /// <summary>Past the threshold — the one thing colour is reserved to say.</summary>
    Alarm,
}

/// <summary>A labelled progress bar (track + fill) with a right-aligned value.</summary>
public sealed class MetricBar : FrameworkElement
{


    private static readonly DependencyProperty DisplayFractionProperty = DependencyProperty.Register("DisplayFraction", typeof(double), typeof(MetricBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(MetricBar),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(string), typeof(MetricBar),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(MetricBar),
        new FrameworkPropertyMetadata(0.0, (d, e) => Motion.Transition((MetricBar)d, DisplayFractionProperty, (double)e.NewValue)));



    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(BarTone), typeof(MetricBar),
        new FrameworkPropertyMetadata(BarTone.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
        nameof(BarHeight), typeof(double), typeof(MetricBar),
        new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowTextProperty = DependencyProperty.Register(
        nameof(ShowText), typeof(bool), typeof(MetricBar),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction
    {
        get => (double)GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    /// <summary>Normal / Hot / Alarm. Drives the fill colour only.</summary>
    public BarTone Tone
    {
        get => (BarTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Track thickness. 3 for the hairline detail bars, 11 for the dashboard load rows.</summary>
    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <summary>False when the label and value are composed in XAML instead of drawn here.</summary>
    public bool ShowText
    {
        get => (bool)GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    private Brush TrackBrush => (Brush)FindResource("TrackBrush");
    private Brush FillBrush => (Brush)FindResource(Tone == BarTone.Alarm ? "AlarmBrush" : "AccentBrush");
    private Brush LabelBrush => (Brush)FindResource("MutedBrush");
    private Brush ValueBrush => (Brush)FindResource("InkBrush");
    private static FontFamily _family = new("pack://application:,,,/Fonts/#Geist, Noto Sans TC");
    private static readonly FontFamily _monoFamily = new("Bahnschrift SemiCondensed, Segoe UI");

    /// <summary>Switches the font used for the label (UI font setting). The value always uses the embedded Geist Mono.</summary>
    public static void SetDefaultFont(FontFamily family) => _family = family;

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 2 || h <= 2) return;

        double barH = Math.Max(1, BarHeight);
        // Square ends past hairline weight: the thick load bars are blocks, not lozenges.
        double radius = barH > 6 ? 0 : barH / 2;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var labelFace = new Typeface(_family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var valueFace = new Typeface(_monoFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(0, 0, w, barH), radius, radius);

        double frac = Math.Clamp((double)GetValue(DisplayFractionProperty), 0, 1);
        double fillW = w * frac;
        if (fillW >= 1)
            dc.DrawRoundedRectangle(FillBrush, null, new Rect(0, 0, fillW, barH), radius, radius);

        if (!ShowText) return;

        double textY = barH + 3;
        var label = new FormattedText(Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, labelFace, 10, LabelBrush, dpi);
        dc.DrawText(label, new Point(0, textY));

        if (!string.IsNullOrEmpty(ValueText))
        {
            var value = new FormattedText(ValueText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, valueFace, 10, ValueBrush, dpi);
            dc.DrawText(value, new Point(w - value.Width, textY));
        }
    }
}



