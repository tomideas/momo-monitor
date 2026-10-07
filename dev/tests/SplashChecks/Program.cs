using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using StatusMonitor;
using StatusMonitor.Settings;
using StatusMonitor.Views;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application();
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/SplashChecks;component/Resources.xaml", UriKind.Relative)));
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                var theme = typeof(App).Assembly.GetType("StatusMonitor.Views.Theme")!;
                foreach (string skin in new[] { "paper", "volt" })
                {
                    theme.GetMethod("Apply")!.Invoke(null, [skin]);
                    Motion.Reduced = false;
                    var splash = new SplashWindow(new AppSettings());
                    splash.Show();
                    await Task.Delay(250);
                    Capture(splash, $"startup-{skin}-first.png");
                    var source = splash.Mascot.Source;
                    await Task.Delay(400);
                    if (SystemParameters.ClientAreaAnimation && ReferenceEquals(source, splash.Mascot.Source))
                        throw new Exception("Playback did not advance.");
                    await splash.AnimationCompleted.WaitAsync(TimeSpan.FromSeconds(9));
                    source = splash.Mascot.Source;
                    await Task.Delay(350);
                    if (!ReferenceEquals(source, splash.Mascot.Source)) throw new Exception("Playback looped.");
                    var timer = (DispatcherTimer)typeof(SplashWindow).GetField("_frameTimer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(splash)!;
                    if (timer.IsEnabled) throw new Exception("Completed timer is still enabled.");
                    Capture(splash, $"startup-{skin}-last.png");
                    splash.Finish();
                }
                Motion.Reduced = true;
                var reduced = new SplashWindow(new AppSettings());
                reduced.Show();
                await Task.Delay(50);
                if (!reduced.AnimationCompleted.IsCompleted) throw new Exception("Reduced motion waits for playback.");
                reduced.Finish();
                Motion.Reduced = false;
                var earlyClose = new SplashWindow(new AppSettings());
                earlyClose.Show();
                await Task.Delay(50);
                earlyClose.Close();
                if (!earlyClose.AnimationCompleted.IsCompleted) throw new Exception("Close left completion pending.");
                Console.WriteLine("PASS: frames advance, play once, hold last, stop timer, reduced motion and early close; both themes rendered.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { app.Dispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run();
    }

    private static void Capture(SplashWindow window, string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine("dev/verification", name));
        png.Save(output);
    }
}

