using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StatusMonitor.Settings;
using StatusMonitor.ViewModels;

namespace StatusMonitor;

public partial class MiniWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _restore;
    private readonly Action _close;
    private readonly bool _preview;
    public MiniWindow(MainViewModel vm, AppSettings settings, Action restore, Action close, bool preview = false)
    {
        _settings = settings; _restore = restore; _close = close; _preview = preview;
        InitializeComponent();
        DataContext = vm;
        Topmost = settings.MiniTopmost;
        FontFamily = AppFont.Resolve(settings.Font);
        // Clamp saved positions to the current virtual desktop after monitor changes.
        double x = settings.MiniLeft ?? SystemParameters.WorkArea.Right - Width - 20;
        double y = settings.MiniTop ?? SystemParameters.WorkArea.Top + 20;
        if (!double.IsFinite(x) || !double.IsFinite(y)) { x = SystemParameters.WorkArea.Right - Width - 20; y = SystemParameters.WorkArea.Top + 20; }
        var screens = System.Windows.Forms.Screen.AllScreens;
        double scaleX = System.Windows.Media.VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
        double scaleY = System.Windows.Media.VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleY;
        var areas = screens.Select(s => new Rect(s.WorkingArea.X / scaleX, s.WorkingArea.Y / scaleY, s.WorkingArea.Width / scaleX, s.WorkingArea.Height / scaleY)).ToArray();
        var area = areas.FirstOrDefault(a => a.Contains(new Point(x + Width / 2, y + Height / 2)));
        if (area.Width <= 0) area = SystemParameters.WorkArea;
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - Width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Height));
        Left = x; Top = y;
    }
    /// <summary>True when the click landed on a control that owns it (the restore button, the pin box).</summary>
    private static bool OnControl(object? source)
    {
        for (var node = source as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase) return true;
        }
        return false;
    }

    private void DragMini(object sender, MouseButtonEventArgs e)
    {
        if (OnControl(e.OriginalSource)) return;
        if (e.ClickCount == 2) { _restore(); return; }
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            if (!_preview) { _settings.MiniLeft = Left; _settings.MiniTop = Top; _settings.Save(); }
        }
    }
    private void RestoreMain(object sender, RoutedEventArgs e) => _restore();

    private void CloseFromMini(object sender, RoutedEventArgs e) => _close();

    /// <summary>
    /// Picks up "always on top" after Settings changed it. The panel is a single long-lived
    /// instance, so the constructor's reading goes stale the moment the setting is edited.
    /// </summary>
    public void ApplyTopmost() => Topmost = _settings.MiniTopmost;
}
