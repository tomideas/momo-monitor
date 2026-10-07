using System.Windows.Media;

namespace StatusMonitor.Settings;

/// <summary>Resolves the selected UI font. Empty value = embedded Geist + Noto Sans TC.</summary>
public static class AppFont
{
    public static readonly string[] Values = { "", "Microsoft JhengHei", "Segoe UI", "PMingLiU" };
    public static readonly string[] FontKeys = { "font_default", "font_jhenghei", "font_segoe", "font_mingliu" };

    public static FontFamily Resolve(string? name) =>
        string.IsNullOrEmpty(name)
            ? new FontFamily("pack://application:,,,/Fonts/#Geist, Noto Sans TC")
            : new FontFamily(name);
}