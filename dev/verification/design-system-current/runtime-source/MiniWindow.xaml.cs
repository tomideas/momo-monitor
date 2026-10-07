using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StatusMonitor.Settings;
using StatusMonitor.ViewModels;
using StatusMonitor.Views;

namespace StatusMonitor;

public partial class MiniWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _restore;
    private readonly Action _close;
    private readonly bool _preview;
    private readonly MascotAnimation _mascotAnimation;
    private readonly MascotAnimation _dragAnimation;
    private Point? _gestureStart;
    private bool _suppressHover;
    public MiniWindow(MainViewModel vm, AppSettings settings, Action restore, Action close, bool preview = false)
    {
        _settings = settings; _restore = restore; _close = close; _preview = preview;
        InitializeComponent();
        _mascotAnimation = new MascotAnimation("Assets/mini-click.frames.zip", frame => MiniMascot.Source = frame);
        _dragAnimation = new MascotAnimation("Assets/mini-drag.frames.zip", frame => MiniMascot.Source = frame);
        _mascotAnimation.Completed += StopMascotAnimations;
        _dragAnimation.Completed += StopMascotAnimations;
        StopMascotAnimations();
        PreviewMouseMove += ContinueMiniGesture;
        PreviewMouseLeftButtonUp += EndMiniGesture;
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) { CancelMiniGesture(); StopMascotAnimations(); }
            else PlayMascot(dragging: false);
        };
        Closed += (_, _) => { CancelMiniGesture(); _mascotAnimation.Dispose(); _dragAnimation.Dispose(); };
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

    private void StopMascotAnimations()
    {
        _dragAnimation.Stop();
        _mascotAnimation.ShowRestFrame();
    }

    private void PlayMascot(bool dragging)
    {
        StopMascotAnimations();
        if (!IsVisible || _preview || _settings.ReduceMotion || !SystemParameters.ClientAreaAnimation)
            return;
        (dragging ? _dragAnimation : _mascotAnimation).PlayOnce();
    }
    /// <summary>Keep button and disk scrollbar gestures out of window dragging.</summary>
    private bool OnControl(object? source)
    {
        for (var node = source as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase && !ReferenceEquals(node, MiniMascotButton) ||
                node is System.Windows.Controls.Primitives.ScrollBar) return true;
        }
        return false;
    }

    private void BeginMiniGesture(object sender, MouseButtonEventArgs e)
    {
        if (OnControl(e.OriginalSource)) return;
        if (e.ClickCount == 2) { CancelMiniGesture(); e.Handled = true; _restore(); return; }
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _gestureStart = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void ContinueMiniGesture(object sender, MouseEventArgs e)
    {
        if (_gestureStart is not Point start) return;
        if (e.LeftButton != MouseButtonState.Pressed) { CancelMiniGesture(); return; }
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        CancelMiniGesture();
        e.Handled = true;
        PlayMascot(dragging: true);
        DragMove();
        if (!_preview) { _settings.MiniLeft = Left; _settings.MiniTop = Top; _settings.Save(); }
    }

    private void EndMiniGesture(object sender, MouseButtonEventArgs e)
    {
        if (_gestureStart is null) return;
        CancelMiniGesture();
        e.Handled = true;
    }

    private void CancelMiniGesture()
    {
        _gestureStart = null;
        _suppressHover = true;
        if (IsMouseCaptured) ReleaseMouseCapture();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => _suppressHover = false));
    }

    private void PlayMascotClick(object sender, RoutedEventArgs e) { e.Handled = true; PlayMascot(dragging: false); }
    private void PlayMascotHover(object sender, MouseEventArgs e)
    {
        if (_suppressHover || _gestureStart is not null || e.LeftButton == MouseButtonState.Pressed || _mascotAnimation.IsPlaying || _dragAnimation.IsPlaying) return;
        PlayMascot(dragging: false);
    }
    private void RestoreMain(object sender, RoutedEventArgs e) => _restore();

    private void CloseFromMini(object sender, RoutedEventArgs e) => _close();

    /// <summary>
    /// Picks up "always on top" after Settings changed it. The panel is a single long-lived
    /// instance, so the constructor's reading goes stale the moment the setting is edited.
    /// </summary>
    public void ApplyTopmost() => Topmost = _settings.MiniTopmost;
}
