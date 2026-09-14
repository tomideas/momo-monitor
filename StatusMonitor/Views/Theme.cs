using System.Windows;
using System.Windows.Media;

namespace StatusMonitor.Views;

/// <summary>
/// Two skins over one skeleton: VOLT (dark, volt accent) and PAPER POP (light, cobalt accent).
/// Both publish the same semantic slots, so XAML never branches on which skin is live.
/// </summary>
internal static class Theme
{
    public static readonly string[] Keys = { "volt", "paper" };

    /// <summary>Maps the seven legacy background keys onto the two skins so saved settings still load.</summary>
    public static string Normalize(string? key) => key switch
    {
        "volt" or "night" => "volt",
        _ => "paper",
    };

    /// <summary>The skin currently applied, so a window shown later can match it.</summary>
    private static string _current = "paper";

    public static void Apply(string key)
    {
        _current = Normalize(key);
        bool dark = _current == "volt";

        // ground / raise / line carry surfaces; ink / muted carry text;
        // pop is the single high-chroma value and alarm replaces it past a threshold.
        // The dark ground is deliberately NOT near-black. #0B0B0B against #F4F3EF ink measured
        // 17.7:1 — pure black under pure white, which is the exact pairing that produces
        // halation: the pupil opens for the dark field, the bright glyphs then overstimulate
        // the retina and bloom into a halo. It is tiring for anyone and painful with
        // astigmatism, which roughly half of people have to some degree. Material Design puts
        // its dark surface at #121212 for the same reason, and reports the OLED power cost of
        // not using true black at about 0.3%.
        // Lifting the ground and easing the ink lands at 13.0:1 — still far above the 7:1 AAA
        // floor, with the bloom gone. Warm-neutral rather than Material's neutral grey, because
        // every other value in this skin is warm. The ground has been raised twice (#0B0B0B →
        // #191917 → #1F1F1C, four times the original luminance); raise / line / dot / hover
        // step up with it, or the surfaces above the ground collapse into it.
        // Neither skin uses an extreme. The light skin's raised surface was #FFFFFF against an
        // #EDEAE1 ground: not just a bigger step than the dark skin's (1.20 against 1.15) but a
        // temperature break, a neutral white slab dropped into a warm palette, which is what
        // made input fields read as glaring. #FAF8F3 keeps the warm cast and lands the step at
        // 1.12, matching the dark skin. The field borders already carry the definition.
        string ground = dark ? "#1F1F1C" : "#EDEAE1";
        string raise = dark ? "#2A2A27" : "#FAF8F3";
        string line = dark ? "#3A3A35" : "#D5D1C5";
        string ink = dark ? "#E6E4DE" : "#0D0D0C";
        // Secondary text, measured against its own ground rather than eyeballed. The history
        // matters: #77746A / #6E6B60 were 4.21 and 4.44, under the AA floor; #9A978C / #545149
        // reached 6.73 and 6.6 and still read as faint, because at 10 DIP with grayscale AA
        // most of a glyph is antialiased edge and never reaches the nominal colour at all.
        // These raise the ceiling to 8.4 (dark) and ~7.7 (light) so the partially covered
        // pixels — which are the majority — land somewhere legible. The dark skin needed the
        // bigger push: light-on-dark partial coverage collapses toward the background. The
        // dark value tracks the ground: it was lifted again alongside #1F1F1C to hold this
        // ratio, since a lighter ground would otherwise eat into it.
        string muted = dark ? "#BCB9AE" : "#4A4740";
        string pop = dark ? "#D4FF00" : "#0A3CFF";
        string onPop = dark ? "#1F1F1C" : "#FAF8F3";
        string alarm = dark ? "#FF3B14" : "#FF2E00";
        string hover = dark ? "#333330" : "#14000000";
        string dot = dark ? "#292925" : "#E2DED3";

        void Set(string name, string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze(); Application.Current.Resources[name] = brush;
        }

        Set("PaperBrush", ground);
        Set("CardBrush", raise);
        Set("HairBrush", line);
        Set("TrackBrush", line);
        Set("InkBrush", ink);
        Set("MutedBrush", muted);
        Set("AccentBrush", pop);
        Set("AmberBrush", pop);
        Set("OnAccentBrush", onPop);
        Set("AlarmBrush", alarm);
        Set("HoverBrush", hover);

        Application.Current.Resources["PopPaper"] = DotPaper(ground, dot);

        // Custom drawn controls resolve the same resources as XAML.
        foreach (Window window in Application.Current.Windows)
        {
            Redraw(window);
            ApplyCaption(window);
        }
    }

    /// <summary>
    /// Paints the window caption in the current skin. Call again from SourceInitialized:
    /// a window created before it is shown has no handle for DWM to act on yet.
    /// </summary>
    public static void ApplyCaption(Window window)
    {
        bool dark = _current == "volt";
        // These must track ground / ink / line above; a caption left on the old near-black
        // would reintroduce exactly the hard edge the lifted ground removes.
        TitleBar.Apply(
            window,
            Parse(dark ? "#1F1F1C" : "#EDEAE1"),
            Parse(dark ? "#E6E4DE" : "#0D0D0C"),
            Parse(dark ? "#3A3A35" : "#D5D1C5"),
            dark);
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>The 20 DIP dot grid the light neo-brutalist ground is built on.</summary>
    private static DrawingBrush DotPaper(string ground, string dot)
    {
        var groundColor = (Color)ColorConverter.ConvertFromString(ground);
        var dotColor = (Color)ColorConverter.ConvertFromString(dot);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(groundColor), null, new RectangleGeometry(new Rect(0, 0, 20, 20))));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(dotColor), null, new EllipseGeometry(new Point(10, 10), 0.8, 0.8)));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 20, 20),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    private static void Redraw(DependencyObject node)
    {
        if (node is UIElement element) element.InvalidateVisual();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Redraw(VisualTreeHelper.GetChild(node, i));
    }
}
