using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StatusMonitor;
using StatusMonitor.Settings;
using StatusMonitor.ViewModels;

internal static class Program
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main()
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "momo-data"));
        Directory.CreateDirectory("dev/verification/mini-animation");
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/MiniAnimationChecks;component/Resources.xaml", UriKind.Relative)));
        app.MainWindow = new Window();
        app.Dispatcher.BeginInvoke(async () =>
        {
            MiniWindow? mini = null;
            var settings = new AppSettings { ReduceMotion = false, MiniTopmost = true };
            var vm = new MainViewModel(settings, persist: false);
            try
            {
                mini = new MiniWindow(vm, settings, () => { }, () => { });
                var image = (Image)mini.FindName("MiniMascot");
                var player = typeof(MiniWindow).GetField("_mascotAnimation", Fields)!.GetValue(mini)!;
                var dragPlayer = typeof(MiniWindow).GetField("_dragAnimation", Fields)!.GetValue(mini)!;
                var playing = player.GetType().GetProperty("IsPlaying")!;
                bool IsPlaying() => (bool)playing.GetValue(player)!;
                bool DragPlaying() => (bool)playing.GetValue(dragPlayer)!;
                var button = (Button)mini.FindName("MiniMascotButton");
                if (typeof(MiniWindow).GetField("_mascotRepeat", Fields) is not null)
                    throw new Exception("Periodic replay timer was not removed.");
                mini.Show();
                await Task.Delay(50);
                if (SystemParameters.ClientAreaAnimation && (!IsPlaying() || DragPlaying())) throw new Exception("Showing mini must play the hover clip once.");
                mini.Activate();
                if (SystemParameters.ClientAreaAnimation)
                {
                    await Task.Delay(6800);
                    await Task.Run(() => SetCursorPos(0, 0));
                    await Task.Delay(100);
                    double initialLeft = mini.Left, initialTop = mini.Top;
                    var hoverPoint = mini.PointToScreen(new Point(40, 35));
                    await Task.Run(() => SetCursorPos((int)hoverPoint.X, (int)hoverPoint.Y));
                    await Task.Delay(50);
                    if (!IsPlaying() || DragPlaying()) throw new Exception("Pointer hover must play the new clip only.");
                    if (mini.Left != initialLeft || mini.Top != initialTop) throw new Exception("Hover moved the window.");
                    var initial = image.Source;
                    await Task.Delay(250);
                    if (ReferenceEquals(initial, image.Source)) throw new Exception("Frames did not advance.");
                    await Task.Delay(2300);
                    Capture(mini, "paper-motion.png");
                    await Task.Delay(4400);
                    if (IsPlaying()) throw new Exception("One-shot playback did not stop.");
                    var rest = image.Source;
                    await Task.Delay(150);
                    if (!ReferenceEquals(rest, image.Source)) throw new Exception("Rest frame changed between cycles.");
                    await Task.Run(async () =>
                    {
                        mouse_event(2, 0, 0, 0, UIntPtr.Zero);
                        await Task.Delay(100);
                        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                    });
                    await Task.Delay(100);
                    if (IsPlaying() || DragPlaying()) throw new Exception("Click must not trigger animation while already hovered.");
                    var insidePoint = mini.PointToScreen(new Point(140, 100));
                    await Task.Run(() => SetCursorPos((int)insidePoint.X, (int)insidePoint.Y));
                    await Task.Delay(100);
                    if (!IsPlaying() || DragPlaying()) throw new Exception("Moving within the panel must replay hover without leaving it.");
                    var clock = (Stopwatch)player.GetType().GetField("_clock", Fields)!.GetValue(player)!;
                    await Task.Delay(200);
                    var elapsed = clock.ElapsedMilliseconds;
                    insidePoint = mini.PointToScreen(new Point(160, 110));
                    await Task.Run(() => SetCursorPos((int)insidePoint.X, (int)insidePoint.Y));
                    await Task.Delay(50);
                    if (clock.ElapsedMilliseconds < elapsed) throw new Exception("Motion restarted a playing hover clip.");
                    await MouseGesture(mini.PointToScreen(new Point(125, 35)), new Vector(-24, 20));
                    await Task.Delay(50);
                    if (IsPlaying() || !DragPlaying()) throw new Exception("A real drag must play the startup clip only.");
                    if (Math.Abs(mini.Left-initialLeft) < 5 && Math.Abs(mini.Top-initialTop) < 5)
                        throw new Exception("Drag did not move the window.");
                    Capture(mini, "paper-drag.png");
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (!IsPlaying() || DragPlaying()) throw new Exception("Keyboard/button click must replace the drag animation.");
                    mini.Hide();
                    if (IsPlaying() || DragPlaying()) throw new Exception("Hidden mini retains animation timers.");
                    mini.Show();
                    if (!IsPlaying() || DragPlaying()) throw new Exception("Returning to mini must play hover once.");
                }
                settings.ReduceMotion = true;
                mini.Hide(); mini.Show();
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (IsPlaying() || DragPlaying()) throw new Exception("Reduced motion must stay static after interaction.");
                foreach (string skin in new[] { "paper", "volt" })
                {
                    typeof(MiniWindow).Assembly.GetType("StatusMonitor.Views.Theme")!.GetMethod("Apply")!.Invoke(null, [skin]);
                    mini.UpdateLayout();
                    if (image.ActualWidth != 48 || image.ActualHeight != 48 || mini.ActualWidth != 278)
                        throw new Exception("Animation changed mini geometry.");
                    if (image.RenderTransform is not ScaleTransform scale || scale.ScaleX != 1.2 || scale.ScaleY != 1.2) throw new Exception("Mascot scale must be 1.2.");
                    Capture(mini, skin + "-rest.png");
                }
                mini.Close();
                if (IsPlaying() || DragPlaying()) throw new Exception("Closed mini retains timers.");
                File.WriteAllText("dev/verification/mini-animation/checks.txt", "PASS: show plays hover once; no periodic timer; real pointer hover plays new clip without moving window; original timing and one-shot idle; real drag moves window and plays startup clip only; keyboard/button click replaces drag playback; hidden/closed timers stop; re-show plays hover once; reduced motion static; 1.2 scale in fixed 48 DIP mascot slot and 278 DIP window; both themes.\n");
                Console.WriteLine("PASS: native whole-panel hover, in-panel motion replay, no restart while playing, click/drag arbitration and idle lifecycle.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { mini?.Close(); vm.Stop(save: false); app.Dispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run();
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    static Task MouseGesture(Point screen, Vector delta) => Task.Run(async () =>
    {
        GetCursorPos(out var previous);
        try
        {
            SetCursorPos((int)screen.X, (int)screen.Y);
            await Task.Delay(80);
            mouse_event(2, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(100);
            if (delta.Length > 0)
            {
                SetCursorPos((int)(screen.X+delta.X/2), (int)(screen.Y+delta.Y/2));
                await Task.Delay(120);
                SetCursorPos((int)(screen.X+delta.X), (int)(screen.Y+delta.Y));
                await Task.Delay(120);
            }
            mouse_event(4, 0, 0, 0, UIntPtr.Zero);
            await Task.Delay(120);
        }
        finally { mouse_event(4,0,0,0,UIntPtr.Zero); SetCursorPos(previous.X, previous.Y); }
    });
    static void Capture(MiniWindow mini, string name)
    {
        mini.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)mini.ActualWidth, (int)mini.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(mini);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create("dev/verification/mini-animation/" + name);
        encoder.Save(stream);
    }
}

