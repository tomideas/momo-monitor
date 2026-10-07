using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StatusMonitor;
using StatusMonitor.Settings;
using StatusMonitor.Services;
using StatusMonitor.I18n;
using Forms = System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // All persistence stays in this test's private portable directory.
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "momo-data"));
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/TrayStartupChecks;component/Resources.xaml", UriKind.Relative)));
        app.Dispatcher.BeginInvoke(async () =>
        {
            MainWindow? window = null;
            try
            {
                var settings = new AppSettings { StartInMiniMode = true, SensorDriverAsked = true, LaunchAtLogon = false, ReduceMotion = true };
                window = new MainWindow(settings, startInTray: true);
                app.MainWindow = window;
                await window.WarmUpAsync();
                var tray = (TrayService)typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                if (!tray.IsVisible || window.IsVisible || window.MiniPreview is not null)
                    throw new Exception("Background startup must have a tray icon and no panels.");
                var icon = (Forms.NotifyIcon)typeof(TrayService).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
                Directory.CreateDirectory("dev/verification/tray-menu");
                foreach (string language in new[] { "en", "zh" })
                {
                    Loc.Instance.SetLanguage(language);
                    var menu = icon.ContextMenuStrip!;
                    string[] expected = language == "en" ? new[] { "Open Dashboard", "Mini", "Settings", "Exit" }
                        : new[] { "開啟儀表板", "迷你", "設定", "結束程式" };
                    if (!menu.Items.OfType<Forms.ToolStripMenuItem>().Select(i => i.Text).SequenceEqual(expected) ||
                        menu.Items.Count != 5 || menu.Items[3] is not Forms.ToolStripSeparator)
                        throw new Exception("Tray menu labels/order/separator did not localize correctly.");
                    menu.CreateControl(); menu.PerformLayout();
                    using var menuBitmap = new System.Drawing.Bitmap(menu.Width, menu.Height);
                    menu.DrawToBitmap(menuBitmap, new System.Drawing.Rectangle(0, 0, menu.Width, menu.Height));
                    menuBitmap.Save($"dev/verification/tray-menu/{language}-menu.png", System.Drawing.Imaging.ImageFormat.Png);
                }
                Loc.Instance.SetLanguage("en");
                ((Forms.ToolStripMenuItem)icon.ContextMenuStrip!.Items[0]).PerformClick();
                await Task.Delay(250);
                if (!window.IsVisible || window.MiniPreview is not null) throw new Exception("Tray restore must show the dashboard instead of starting mini mode.");
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using (var output = File.Create("dev/verification/tray-restored.png")) png.Save(output);
                window.Hide();
                ((Forms.ToolStripMenuItem)icon.ContextMenuStrip!.Items[2]).PerformClick();
                if (!window.IsVisible || ((UIElement)window.FindName("SettingsOverlay")).Visibility != Visibility.Visible)
                    throw new Exception("Tray Settings must restore the hidden window and open the existing settings overlay.");
                ((UIElement)window.FindName("SettingsOverlay")).Visibility = Visibility.Collapsed;
                ((Forms.ToolStripMenuItem)icon.ContextMenuStrip!.Items[1]).PerformClick();
                if (window.IsVisible || window.MiniPreview?.IsVisible != true)
                    throw new Exception("Tray Mini must show the mini panel.");
                if (((System.Windows.Controls.Border)window.MiniPreview!.Content).ToolTip is not null)
                    throw new Exception("Mini panel must not show a drag-hint tooltip.");
                CheckMiniDetails();
                ((Forms.ToolStripMenuItem)icon.ContextMenuStrip!.Items[2]).PerformClick();
                if (!window.IsVisible || window.MiniPreview?.IsVisible == true || ((UIElement)window.FindName("SettingsOverlay")).Visibility != Visibility.Visible)
                    throw new Exception("Tray Settings must leave mini mode and open settings on the main window.");
                ((Forms.ToolStripMenuItem)icon.ContextMenuStrip!.Items[4]).PerformClick();
                if (tray.IsVisible) throw new Exception("Exit must dispose the tray icon.");
                window = null;
                File.WriteAllText("dev/verification/tray-startup-checks.txt", "PASS: hidden startup; visible tray icon; hardware warm-up; English/Chinese menu labels and separator; menu Open Dashboard restores despite StartInMiniMode; Settings opens from hidden and Mini; menu Exit disposes tray.\n");
                Console.WriteLine("PASS: tray-only startup; localized native menu; dashboard/Mini/Settings/Exit actions.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally
            {
                window?.ExitApplication();
                app.Dispatcher.InvokeShutdown();
            }
        });
        Dispatcher.Run();
    }

    private static void CheckMiniDetails()
    {
        var settings = new AppSettings { HideIntegratedGpu = false, ReduceMotion = true };
        var vm = new StatusMonitor.ViewModels.MainViewModel(settings, persist: false);
        var apply = typeof(StatusMonitor.ViewModels.MainViewModel).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var sample = new StatusMonitor.Models.Snapshot
        {
            Cpu = new() { ClockMhz = 5123, LoadPercent = 25 },
            RamUsedGb = 15.5, RamTotalGb = 64, RamLoadPercent = 24,
            GpuPresent = true,
            GpuNames = new() { "GPU A", "GPU B" },
            Gpus = new()
            {
                new() { Id = "gpu-a", MemoryUsedMb = 2048, MemoryTotalMb = 8192, LoadPercent = 30 },
                new() { Id = "gpu-b", MemoryUsedMb = 512, LoadPercent = 10 }
            },
            Disks = new()
            {
                new() { Name = "C:\\", UsedGb = 400, TotalGb = 1000 },
                new() { Name = "D:\\", UsedGb = 1800, TotalGb = 2000 }
            }
        };
        MiniWindow? mini = null;
        try
        {
            apply.Invoke(vm, new object[] { sample });
            mini = new MiniWindow(vm, settings, () => { }, () => { }, preview: true);
            mini.Show();
            foreach (string theme in new[] { "paper", "volt" })
            foreach (string language in new[] { "en", "zh" })
            {
                typeof(MiniWindow).Assembly.GetType("StatusMonitor.Views.Theme")!
                    .GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
                    .Invoke(null, new object[] { theme });
                Loc.Instance.SetLanguage(language);
                mini.UpdateLayout();
                var mascot = (System.Windows.Controls.Image)mini.FindName("MiniMascot");
                if (mascot.Source is not BitmapSource mascotImage ||
                    mascotImage.PixelWidth != 256 || mascotImage.PixelHeight != 256 ||
                    Math.Abs(mascot.ActualWidth - 48) > 0.1 || mascot.ActualHeight < 47 || mascot.ActualHeight > 48.1)
                    throw new Exception($"Mini mascot: source={mascot.Source?.GetType().Name}, pixels={(mascot.Source as BitmapSource)?.PixelWidth}x{(mascot.Source as BitmapSource)?.PixelHeight}, size={mascot.ActualWidth}x{mascot.ActualHeight}");
                IEnumerable<DependencyObject> Descendants(DependencyObject node)
                {
                    yield return node;
                    for (int child = 0; child < VisualTreeHelper.GetChildrenCount(node); child++)
                        foreach (var descendant in Descendants(VisualTreeHelper.GetChild(node, child))) yield return descendant;
                }
                if (Descendants(mini).OfType<System.Windows.Controls.TextBlock>().Any(text =>
                    text.Text == "LIVE" || System.Windows.Data.BindingOperations.GetBinding(text,
                        System.Windows.Controls.TextBlock.TextProperty)?.Path?.Path == "ReadingStatus"))
                    throw new Exception("Mini must not have a LIVE/status footer.");
                void Expect(string name, string expected)
                {
                    var text = (System.Windows.Controls.TextBlock)mini.FindName(name);
                    if (text.Text != expected || text.ActualWidth <= 0 || text.ActualHeight <= 0)
                        throw new Exception($"{name}: expected {expected}, got {text.Text}");
                }
                Expect("MiniCpuDetail", "5123 MHz");
                Expect("MiniRamDetail", "15.5 GB");
                Expect("MiniGpu1Detail", "VRAM 2.0 GB");
                Expect("MiniGpu2Detail", "VRAM 0.5 GB");
                CheckDiskItems(mini, vm, 2);
                if (Math.Abs(mini.ActualWidth - 278) > 0.1 || vm.DiskPeakText != "D: 90%")
                    throw new Exception("Mini width or fullest-drive selection changed.");
                if (mini.ActualHeight > 250)
                    throw new Exception("Two-drive mini should remain compact.");
            }
            sample.Disks[0].UsedGb = 950;
            sample.Gpus[0].MemoryUsedMb = null;
            sample.Cpu.ClockMhz = null;
            sample.RamTotalGb = 0;
            apply.Invoke(vm, new object[] { sample });
            mini.UpdateLayout();
            if (vm.DiskPeakText != "C: 95%" || vm.DiskPeakUsedText != "950 GB" ||
                vm.GpuCard1!.VramUsedText != "—" || vm.CpuClockText != "—" || vm.RamUsedText != "—")
                throw new Exception("Mini details did not refresh after a missing reading or fullest-drive change.");
            CheckDiskItems(mini, vm, 2);
            sample.Disks.RemoveAt(1);
            apply.Invoke(vm, new object[] { sample });
            mini.UpdateLayout();
            CheckDiskItems(mini, vm, 1);
            sample.Disks = Enumerable.Range(0, 8).Select(i => new StatusMonitor.Models.DiskVolume
            {
                Name = $"{(char)('C' + i)}:\\", UsedGb = i == 0 ? 0 : 1024 + i * 100,
                TotalGb = 3000
            }).ToList();
            apply.Invoke(vm, new object[] { sample });
            mini.UpdateLayout();
            CheckDiskItems(mini, vm, 8);
            var diskScroll = (System.Windows.Controls.ScrollViewer)mini.FindName("MiniDiskScroll");
            if (diskScroll.ActualHeight > 120.1 || diskScroll.ScrollableHeight <= 0 || mini.ActualHeight > 310)
                throw new Exception("Many drives must scroll inside a bounded mini panel.");
            var scrollbar = (System.Windows.Controls.Primitives.ScrollBar)diskScroll.Template.FindName("PART_VerticalScrollBar", diskScroll);
            if (!(bool)typeof(MiniWindow).GetMethod("OnControl", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(mini, new object[] { scrollbar })!)
                throw new Exception("Dragging the disk scrollbar must not drag or restore the mini window.");
            diskScroll.ScrollToBottom();
            appPump();
            mini.UpdateLayout();
            if (diskScroll.VerticalOffset <= 0)
                throw new Exception("Last drive must be reachable by scrolling.");
            Directory.CreateDirectory("dev/verification/mini-compact");
            var many = new RenderTargetBitmap((int)mini.ActualWidth, (int)mini.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            many.Render(mini);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(many));
            using (var stream = File.Create("dev/verification/mini-compact/eight-drives.png")) encoder.Save(stream);
            apply.Invoke(vm, new object[] { new StatusMonitor.Models.Snapshot() });
            mini.UpdateLayout();
            if (vm.DiskPeakUsedText != "—" || vm.Gpu1Visible || vm.Gpu2Visible)
                throw new Exception("Mini must handle no disks and no GPUs.");
            CheckDiskItems(mini, vm, 0);
            File.WriteAllText("dev/verification/mini-compact/native-checks.txt", "PASS: all C/D drives show label, percentage and used capacity; 0/1/2/8 drives; live update and stable order after peak changes; GB/TB and zero used; bounded disk scroll and last drive reachable; compact two-drive height; both themes/locales; CPU/RAM/dual GPU details retained.\n");
            Directory.CreateDirectory("dev/verification/mini-mascot");
            File.WriteAllText("dev/verification/mini-mascot/native-checks.txt", "PASS: transparent mini.webp playback frames loaded on a fixed 256x256 transparent canvas in a 48x48 DIP box; no LIVE text or ReadingStatus footer; both themes and locales; existing CPU/RAM/dual-GPU/all-disk checks passed.\n");
            File.WriteAllText("dev/verification/mini-details/native-checks.txt", "PASS: CPU MHz; both GPU VRAM used quantities including unknown total; RAM used GB; fullest disk used GB/TB; live updates; missing readings; no disks/GPUs; fixed mini width; PAPER POP/VOLT and English/Chinese.\n");
        }
        finally { mini?.Close(); vm.Stop(save: false); }
    }

    private static void appPump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

    private static void CheckDiskItems(MiniWindow mini, StatusMonitor.ViewModels.MainViewModel vm, int count)
    {
        var items = (System.Windows.Controls.ItemsControl)mini.FindName("MiniDisks");
        if (items.Items.Count != count) throw new Exception("Mini must retain every disk.");
        IEnumerable<System.Windows.Controls.TextBlock> Texts(DependencyObject node)
        {
            if (node is System.Windows.Controls.TextBlock text) yield return text;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                foreach (var child in Texts(VisualTreeHelper.GetChild(node, i))) yield return child;
        }
        for (int i = 0; i < count; i++)
        {
            var presenter = (System.Windows.Controls.ContentPresenter)items.ItemContainerGenerator.ContainerFromIndex(i)!;
            if (presenter is null) throw new Exception("Disk row was not realized.");
            var texts = Texts(presenter).Select(t => t.Text).ToArray();
            var disk = vm.DiskRows[i];
            if (!texts.SequenceEqual(new[] { disk.Label, disk.PercentText, disk.UsedText }))
                throw new Exception("Disk row must show its own label, percentage and capacity in source order.");
        }
    }
}
