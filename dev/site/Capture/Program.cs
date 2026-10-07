using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using StatusMonitor;
using StatusMonitor.I18n;
using StatusMonitor.Settings;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (!args.Contains("--render")) throw new InvalidOperationException("Use --render to ensure read-only preview mode.");
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application();
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Capture;component/Resources.xaml", UriKind.Relative)));
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Dispatcher.BeginInvoke(async () =>
        {
            MainWindow? window = null;
            try
            {
                Loc.Instance.SetLanguage("en");
                window = new MainWindow(new AppSettings { Language = "en", BackgroundTheme = "paper", ReduceMotion = true });
                window.Height = 900;
                window.Show();
                await Task.Delay(3000);
                window.ShowSettingsPanel();
                window.UpdateLayout();
                ((TabItem)window.FindName("MonitoringTab")).IsSelected = true;
                window.UpdateLayout();
                ScrollTo(window, "EnableAlertsBox");
                Capture(window, "alert-settings-en.png");
                // The first settings tab owns runtime/tray preferences further down its scroll area.
                ((TabControl)window.FindName("SettingsTabs")).SelectedIndex = 0;
                window.UpdateLayout();
                ScrollTo(window, "RefreshCombo");
                Capture(window, "runtime-settings-en.png");
                Console.WriteLine("PASS: read-only English WPF captures of thresholds and runtime preferences.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { window?.Close(); app.Dispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run();
    }

    static void ScrollTo(MainWindow window, string name)
    {
        var target = (FrameworkElement)window.FindName(name);
        DependencyObject? parent = target;
        while (parent is not ScrollViewer) parent = VisualTreeHelper.GetParent(parent!);
        var scroll = (ScrollViewer)parent;
        scroll.ScrollToVerticalOffset(scroll.VerticalOffset + target.TransformToAncestor(scroll).Transform(new Point(0, 0)).Y);
        window.UpdateLayout();
    }

    static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine("site", "assets", "images", name));
        png.Save(output);
    }
}
