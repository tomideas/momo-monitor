using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace StatusMonitor.Views;

/// <summary>
/// Tints the real Windows caption so it matches the skin instead of sitting on the window
/// as a strip of system chrome. Needs Windows 11 (build 22000+); on anything older the DWM
/// calls return a failure HRESULT and the system caption is simply left alone.
/// Keeping the native caption preserves Snap Layouts, drag, and the DPI behaviour that a
/// hand-drawn title bar would have to reimplement.
/// </summary>
internal static class TitleBar
{
    private const int UseImmersiveDarkMode = 20;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>DWM takes 0x00BBGGRR — the reverse of the usual RGB byte order.</summary>
    private static int Bgr(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>
    /// Returns false when the window has no handle yet (it has not been shown), so the caller
    /// can retry from SourceInitialized.
    /// </summary>
    public static bool Apply(Window window, Color caption, Color text, Color border, bool dark)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;

        int darkMode = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref darkMode, sizeof(int));

        int captionValue = Bgr(caption);
        int textValue = Bgr(text);
        int borderValue = Bgr(border);
        DwmSetWindowAttribute(hwnd, CaptionColor, ref captionValue, sizeof(int));
        DwmSetWindowAttribute(hwnd, TextColor, ref textValue, sizeof(int));
        DwmSetWindowAttribute(hwnd, BorderColor, ref borderValue, sizeof(int));
        return true;
    }
}
