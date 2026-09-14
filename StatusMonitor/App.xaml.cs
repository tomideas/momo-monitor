using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;
using StatusMonitor.I18n;
using StatusMonitor.Services;
using StatusMonitor.Settings;

namespace StatusMonitor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Fan control is the only thing here that outlives a bad exit: a card left on a
        // software curve keeps that speed until the driver resets. Every path out of the
        // process therefore hands the fans back, not just the tidy one.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            RevertFans();
            LogError(args.ExceptionObject as Exception);
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RevertFans();
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true;
        };

        if (Array.IndexOf(e.Args, "--dump") >= 0)
        {
            RunDump(e.Args);
            Shutdown();
            return;
        }

        if (Array.IndexOf(e.Args, "--diag") >= 0)
        {
            RunDiag(e.Args);
            Shutdown();
            return;
        }

        if (Array.IndexOf(e.Args, "--profile") >= 0)
        {
            RunProfile(e.Args);
            Shutdown();
            return;
        }

        if (Array.IndexOf(e.Args, "--fan-test") >= 0)
        {
            RunFanTest(e.Args);
            Shutdown();
            return;
        }

        if (Array.IndexOf(e.Args, "--power-log") >= 0)
        {
            RunPowerLog(e.Args);
            Shutdown();
            return;
        }

        try
        {
            var settings = AppSettings.Load();
            Loc.Instance.SetLanguage(settings.Language);

            if (Array.IndexOf(e.Args, "--render") >= 0)
            {
                settings = new AppSettings { Language = Array.IndexOf(e.Args, "--english") >= 0 ? "en" : "zh", ReduceMotion = true };
                Loc.Instance.SetLanguage(settings.Language);
                RenderWindow(settings, e.Args);
                return;
            }

            Launch(settings);
        }
        catch (Exception ex)
        {
            LogError(ex);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Shows the splash, warms the sensors up behind it, then swaps in the dashboard.
    /// <para>
    /// Order matters twice here. The main window is shown before the splash closes, so the
    /// app never has zero windows open — with the default OnLastWindowClose shutdown mode
    /// that gap would end the process. And MainWindow is assigned to
    /// <see cref="Application.MainWindow"/> explicitly, because WPF otherwise leaves it
    /// pointing at the closed splash.
    /// </para>
    /// </summary>
    private async void Launch(AppSettings settings)
    {
        SplashWindow? splash = null;
        try
        {
            // The splash draws from the theme, which MainWindow's constructor normally
            // applies; it has to be in place before anything is shown.
            Views.Theme.Apply(settings.BackgroundTheme);
            Views.Motion.Reduced = settings.ReduceMotion;

            splash = new SplashWindow(settings);
            splash.Show();

            var window = new MainWindow(settings);
            await window.WarmUpAsync();

            MainWindow = window;
            window.Show();
            window.Activate();
            splash.Finish();
            splash = null;
        }
        catch (Exception ex)
        {
            LogError(ex);
            splash?.Finish();
            Shutdown(1);
        }
    }

    /// <summary>Renders the whole dashboard to a PNG (for visual verification).</summary>
    private void RenderWindow(AppSettings settings, string[] args)
    {
        string outPath = "render.png";
        int idx = Array.IndexOf(args, "--render");
        if (idx >= 0 && idx + 1 < args.Length) outPath = args[idx + 1];

        int themeIndex = Array.IndexOf(args, "--theme");
        if (themeIndex >= 0 && themeIndex + 1 < args.Length) settings.BackgroundTheme = args[themeIndex + 1];

        // Lets the "record only starts on <date>" state be captured, not just reasoned about.
        int periodIndex = Array.IndexOf(args, "--period");
        if (periodIndex >= 0 && periodIndex + 1 < args.Length &&
            Enum.TryParse(args[periodIndex + 1], true, out Services.EnergyPeriod period))
            settings.EnergyPeriod = period;

        // The splash is its own window, so it gets its own branch rather than a mode inside
        // the dashboard render. Without this it would be the one screen nobody could check.
        if (args.Contains("--splash"))
        {
            Views.Theme.Apply(settings.BackgroundTheme);
            Views.Motion.Reduced = settings.ReduceMotion;
            var splash = new SplashWindow(settings);
            splash.Show();
            var splashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            splashTimer.Tick += (_, _) =>
            {
                splashTimer.Stop();
                try { RenderContent(splash, outPath, "dash"); }
                catch (Exception ex) { LogError(ex); Shutdown(1); return; }
                Shutdown();
            };
            splashTimer.Start();
            return;
        }

        var window = new MainWindow(settings);
        int widthIndex = Array.IndexOf(args, "--width");
        if (widthIndex >= 0 && widthIndex + 1 < args.Length && double.TryParse(args[widthIndex + 1], out double width)) window.Width = width;
        // Height matters as much as width now that the window resizes down to 460x330: the
        // settings dialog is the one view that cannot simply scroll away a shortfall.
        int heightIndex = Array.IndexOf(args, "--height");
        if (heightIndex >= 0 && heightIndex + 1 < args.Length && double.TryParse(args[heightIndex + 1], out double height)) window.Height = height;
        window.Show();

        string mode = Array.IndexOf(args, "--settings") >= 0 ? "settings"
                    : Array.IndexOf(args, "--info") >= 0 ? "info"
                    : Array.IndexOf(args, "--fans") >= 0 ? "fans"
                    : "dash";
        if (mode == "settings") window.ShowSettingsPanel();
        else if (mode == "info") window.ShowInfoPanel();

        int delayIndex = Array.IndexOf(args, "--wait-seconds");
        int delay = delayIndex >= 0 && delayIndex + 1 < args.Length && int.TryParse(args[delayIndex + 1], out int requestedDelay) ? Math.Clamp(requestedDelay, 3, 30) : 3;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                window.UpdateLayout();
                if (args.Contains("--verify-features"))
                    File.WriteAllText(outPath + ".checks.txt", window.VerifyFeatureInteractions());
                if (mode == "dash")
                {
                    // The dashboard is a single column of load rows sharing one baseline, so the
                    // old multi-column gap/height assertions no longer describe anything. What
                    // matters now is that every row starts its bar at the same x.
                    var bars = Descendants<Views.MetricBar>(window)
                        .Where(b => b.IsVisible && b.BarHeight > 6)
                        .Select(b => b.TransformToAncestor(window).Transform(new Point(0, 0)).X)
                        .ToList();
                    if (bars.Count == 0) throw new InvalidOperationException("Dashboard must render load bars.");
                    File.WriteAllText(outPath + ".json", JsonSerializer.Serialize(new { window.Title, Width = window.ActualWidth,
                        BarCount = bars.Count, BarLeft = bars.Select(x => Math.Round(x, 1)) }, new JsonSerializerOptions { WriteIndented = true }));
                }
                if (mode == "info")
                {
                    var cards = Descendants<System.Windows.Controls.Border>(window)
                        .Where(b => b.DataContext is Models.InfoCard && b.Height == 280).ToList();
                    if (cards.Count == 0 || cards.Any(b => Math.Abs(b.ActualHeight - 280) > 0.1) ||
                        cards.Any(b => Math.Abs(b.ActualWidth - cards[0].ActualWidth) > 0.1))
                        throw new InvalidOperationException("Info cards must have equal widths and 280 DIP heights.");
                    File.WriteAllText(outPath + ".json", JsonSerializer.Serialize(new { window.Title, Width = window.ActualWidth,
                        Cards = cards.Select(b => new { ((Models.InfoCard)b.DataContext).Title, b.ActualWidth, b.ActualHeight }) }, new JsonSerializerOptions { WriteIndented = true }));
                    if (args.Contains("--details"))
                    {
                        var button = Descendants<System.Windows.Controls.Button>(window).First(b => b.DataContext is Models.InfoCard);
                        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    }
                }
                if (args.Contains("--trends"))
                {
                    ((FrameworkElement)window.FindName("TrendsCard")).BringIntoView();
                    window.UpdateLayout();
                }
                if (args.Contains("--closing"))
                {
                    window.ShowClosePrompt();
                    window.UpdateLayout();
                }
                if (args.Contains("--fans"))
                {
                    window.ShowFansPanel();
                    if (args.Contains("--fan-custom")) window.PreviewFanCustom();
                    window.UpdateLayout();
                }
                if (args.Contains("--monitoring"))
                {
                    ((System.Windows.Controls.TabItem)window.FindName("MonitoringTab")).IsSelected = true;
                    ((FrameworkElement)window.FindName("GpuCombo")).BringIntoView();
                    window.UpdateLayout();
                }
                if (args.Contains("--bottom"))
                {
                    ((System.Windows.Controls.ScrollViewer)window.FindName("DashboardView")).ScrollToEnd();
                    window.UpdateLayout();
                }
                if (args.Contains("--mini"))
                {
                    window.ShowMiniMode();
                    RenderContent(window.MiniPreview!, outPath, "mini");
                }
                else RenderContent(window, outPath, mode);
            }
            catch (Exception ex) { LogError(ex); Shutdown(1); return; }
            Shutdown();
        };
        timer.Start();
    }

    private static void RenderContent(Window window, string outPath, string mode)
    {
        window.UpdateLayout();

        FrameworkElement content;
        int w, h;
        if (mode == "settings")
        {
            content = (FrameworkElement)window.Content;
            w = (int)Math.Ceiling(content.ActualWidth);
            h = (int)Math.Ceiling(content.ActualHeight);
        }
        else if (mode == "info")
        {
            content = (FrameworkElement)window.Content;
            w = (int)Math.Ceiling(content.ActualWidth);
            h = (int)Math.Ceiling(content.ActualHeight);
        }
        else
        {
            content = (FrameworkElement)window.Content;
            w = (int)Math.Ceiling(content.ActualWidth);
            h = (int)Math.Ceiling(content.ActualHeight);
        }
        if (w < 10 || h < 10) { w = 1160; h = 900; }

        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outPath);
        encoder.Save(stream);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    /// <summary>Dumps every sensor LibreHardwareMonitor can see (for troubleshooting).</summary>
    private static void RunDiag(string[] args)
    {
        string outPath = "diag.txt";
        int idx = Array.IndexOf(args, "--diag");
        if (idx >= 0 && idx + 1 < args.Length) outPath = args[idx + 1];

        var lines = new List<string>();
        using var hub = new Sensors.HardwareMonitorHub();
        hub.Open(everything: true);
        hub.Update();
        lines.Add($"available={hub.Available} error={hub.Error}");

        // A sensor that carries an IControl is one the machine will let software drive. That is
        // the only thing that decides whether fan control is possible here, so it is reported
        // explicitly rather than inferred from the presence of a fan reading.
        static string Describe(ISensor s) =>
            $"{s.SensorType}|{s.Name}={s.Value}" + (s.Control is null ? "" :
                $"  [CONTROLLABLE mode={s.Control.ControlMode} sw={s.Control.SoftwareValue} min={s.Control.MinSoftwareValue} max={s.Control.MaxSoftwareValue}]");

        void Dump(HardwareType type)
        {
            foreach (var hw in hub.All(type).DefaultIfEmpty(null))
            {
                if (hw is null) { lines.Add($"[{type}] (none)"); return; }
                lines.Add($"[{type}] {hw.Name}");
                foreach (var s in hw.Sensors) lines.Add("  " + Describe(s));
                foreach (var sub in hw.SubHardware)
                {
                    lines.Add($"  [sub] {sub.Name}");
                    foreach (var s in sub.Sensors) lines.Add("    " + Describe(s));
                }
            }
        }

        Dump(HardwareType.Cpu);
        Dump(HardwareType.Motherboard);
        Dump(HardwareType.Storage);
        Dump(HardwareType.GpuNvidia);
        Dump(HardwareType.GpuAmd);
        Dump(HardwareType.GpuIntel);
        Dump(HardwareType.Cooler);

        int controls = hub.All(HardwareType.Cpu).Concat(hub.All(HardwareType.Motherboard))
            .Concat(hub.All(HardwareType.GpuNvidia)).Concat(hub.All(HardwareType.GpuAmd))
            .Concat(hub.All(HardwareType.GpuIntel)).Concat(hub.All(HardwareType.Cooler))
            .SelectMany(Sensors.HardwareMonitorHub.SelfAndSub)
            .SelectMany(h => h.Sensors).Count(s => s.Control is not null);
        lines.Insert(1, $"controllable sensors found = {controls}");
        File.WriteAllLines(outPath, lines);
    }

    /// <summary>
    /// Proves the write path on real hardware: takes a fan, raises it, checks the tachometer
    /// moved, then hands it back and checks it recovered.
    /// <para>
    /// Only ever raises the speed. Driving a fan <em>down</em> to observe a change would be
    /// testing the one direction that can overheat a part, so the test does not do it — a
    /// higher speed is always thermally safe, and it demonstrates the same mechanism.
    /// </para>
    /// </summary>
    private static void RunFanTest(string[] args)
    {
        int idx = Array.IndexOf(args, "--fan-test");
        double percent = idx >= 0 && idx + 1 < args.Length && double.TryParse(args[idx + 1], out double p) ? p : 55;

        var lines = new List<string>();
        using var hub = new Sensors.HardwareMonitorHub();
        hub.Open();
        hub.Update();

        var fans = new Services.FanControlService();
        fans.Discover(hub);
        lines.Add($"controllable fans = {fans.Targets.Count}");
        if (fans.Targets.Count == 0) { WriteFanLog(args, lines); return; }

        var target = fans.Targets[0];
        lines.Add($"target = {target.HardwareName} / {target.SensorName}  range {target.MinPercent:0}-{target.MaxPercent:0}%");

        double Rpm()
        {
            hub.Update();
            Thread.Sleep(2500);
            hub.Update();
            foreach (var hw in hub.All(LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia)
                         .Concat(hub.All(LibreHardwareMonitor.Hardware.HardwareType.GpuAmd)))
                foreach (var s in hw.Sensors)
                    if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Fan && s.Value is not null)
                        return s.Value.Value;
            return double.NaN;
        }

        double before = Rpm();
        lines.Add($"before          : {target.Sensor.Value:0}%  {before:0} RPM  mode={target.Control.ControlMode}");

        // Never below the floor the hardware itself reports, and never a reduction.
        double requested = Math.Clamp(Math.Max(percent, target.Sensor.Value ?? 0), target.MinPercent, target.MaxPercent);
        target.Control.SetSoftware((float)requested);
        double raised = Rpm();
        lines.Add($"set {requested:0}%        : {target.Sensor.Value:0}%  {raised:0} RPM  mode={target.Control.ControlMode}");

        target.Control.SetDefault();
        double after = Rpm();
        lines.Add($"reverted        : {target.Sensor.Value:0}%  {after:0} RPM  mode={target.Control.ControlMode}");

        lines.Add(raised > before + 50 ? "RESULT: software control moved the fan" : "RESULT: no measurable change");
        lines.Add(target.Control.ControlMode.ToString() == "Software"
            ? "WARNING: control mode still reads Software after SetDefault"
            : "RESULT: control handed back to firmware");
        fans.RevertAll();
        WriteFanLog(args, lines);
    }

    private static void WriteFanLog(string[] args, List<string> lines)
    {
        string outPath = "fan-test.txt";
        int outIdx = Array.IndexOf(args, "--out");
        if (outIdx >= 0 && outIdx + 1 < args.Length) outPath = args[outIdx + 1];
        File.WriteAllLines(outPath, lines);
    }

    /// <summary>
    /// Logs total watts once a second. Decimating this series offline is how the cost of a
    /// slower refresh gets a number instead of a shrug: the question "how much accuracy does
    /// a 5 second interval lose?" is answerable only against a reference series.
    /// </summary>
    private static void RunPowerLog(string[] args)
    {
        int idx = Array.IndexOf(args, "--power-log");
        int seconds = idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int s) ? Math.Clamp(s, 10, 240) : 120;

        string outPath = "power-log.csv";
        int outIdx = Array.IndexOf(args, "--out");
        if (outIdx >= 0 && outIdx + 1 < args.Length) outPath = args[outIdx + 1];

        var settings = AppSettings.Load();
        using var service = new MonitorService(settings);
        service.Sample();

        var rows = new List<string> { "seconds,watts" };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            Thread.Sleep(1000);
            var snap = service.Sample();
            rows.Add($"{clock.Elapsed.TotalSeconds:0.000},{snap.TotalWatts:0.0000}");
        }
        File.WriteAllLines(outPath, rows);
    }

    /// <summary>
    /// Times each part of one sampling round, so "is it heavy?" is answered with numbers
    /// instead of intuition. Calls the same sensor entry points the running app calls.
    /// </summary>
    private static void RunProfile(string[] args)
    {
        int idx = Array.IndexOf(args, "--profile");
        int rounds = idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int n) ? Math.Clamp(n, 1, 60) : 8;
        var lines = new List<string> { $"rounds={rounds}  cores={Environment.ProcessorCount}" };

        // Milliseconds per call, discarding the first round: the first touch of a perf
        // counter category or a sensor opens handles that a steady-state tick does not.
        void Time(string label, Action action)
        {
            var samples = new List<double>();
            var sw = new System.Diagnostics.Stopwatch();
            for (int i = 0; i <= rounds; i++)
            {
                sw.Restart();
                try { action(); } catch (Exception ex) { lines.Add($"{label,-34} FAILED {ex.GetType().Name}"); return; }
                sw.Stop();
                if (i > 0) samples.Add(sw.Elapsed.TotalMilliseconds);
                Thread.Sleep(200);
            }
            samples.Sort();
            lines.Add($"{label,-34} median {samples[samples.Count / 2],8:0.0} ms   max {samples[^1],8:0.0} ms");
        }

        var settings = AppSettings.Load();
        using var hub = new Sensors.HardwareMonitorHub();
        hub.Open();
        hub.Update();
        Time("hub.Update (all hardware)", () => hub.Update());

        // Per node, because the app only ever reads Cpu, Motherboard and the GPUs — whatever
        // the other nodes cost is being paid for nothing.
        foreach (var type in new[]
        {
            HardwareType.Cpu, HardwareType.Motherboard, HardwareType.Memory, HardwareType.Storage,
            HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel, HardwareType.Cooler,
            HardwareType.Psu, HardwareType.Battery, HardwareType.Network,
        })
        {
            if (!hub.All(type).Any()) continue;
            var nodes = hub.All(type).ToList();
            Time($"  hub.Update {type}", () =>
            {
                foreach (var hw in nodes)
                    foreach (var node in Sensors.HardwareMonitorHub.SelfAndSub(hw)) node.Update();
            });
        }

        Time("Process.GetProcesses", () =>
        {
            foreach (var p in System.Diagnostics.Process.GetProcesses()) p.Dispose();
        });

        Time("GPU Engine GetInstanceNames", () =>
            _ = new System.Diagnostics.PerformanceCounterCategory("GPU Engine").GetInstanceNames());

        var process = new Sensors.ProcessSensor();
        process.Sample(1.0);
        Time("ProcessSensor.Sample (whole)", () => process.Sample(1.0));

        // Splits the two halves of that number. The per-process reads and the GPU Engine
        // counters are separate costs and only one of them can be cut without losing a column.
        Time("  per-process property reads", () =>
        {
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try { _ = p.ProcessName; _ = p.TotalProcessorTime; _ = p.Handle; _ = p.WorkingSet64; }
                catch { }
                finally { p.Dispose(); }
            }
        });

        var counters = new Dictionary<string, System.Diagnostics.PerformanceCounter>(StringComparer.OrdinalIgnoreCase);
        Time("  GPU Engine counters NextValue", () =>
        {
            var category = new System.Diagnostics.PerformanceCounterCategory("GPU Engine");
            foreach (string name in category.GetInstanceNames())
            {
                if (!name.Contains("engtype", StringComparison.OrdinalIgnoreCase)) continue;
                if (!counters.TryGetValue(name, out var counter))
                {
                    try
                    {
                        counter = new System.Diagnostics.PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                        counter.NextValue();
                    }
                    catch { continue; }
                    counters[name] = counter;
                }
                try { counter.NextValue(); } catch { }
            }
        });
        lines.Add($"  GPU Engine counters held        {counters.Count}");

        // The candidate replacement: one query for the whole category instead of 427 separate
        // NextValue calls. Worth knowing before deciding to sample the column less often.
        Time("  GPU Engine ReadCategory (1 call)", () =>
            _ = new System.Diagnostics.PerformanceCounterCategory("GPU Engine").ReadCategory());
        foreach (var counter in counters.Values) counter.Dispose();
        lines.Add($"  processes seen                  {System.Diagnostics.Process.GetProcesses().Length}");

        using var storage = new Sensors.StorageSensor();
        var scratch = new Models.Snapshot();
        Time("StorageSensor.Read", () => storage.Read(scratch));

        var network = new Sensors.NetworkSensor();
        Time("NetworkSensor.Read", () => network.Read(scratch, 1.0));

        using var clock = new Sensors.CpuClockSensor();
        Time("CpuClockSensor.Read", () => clock.Read());

        using var service = new MonitorService(settings);
        service.Sample();
        Time("MonitorService.Sample (whole tick)", () => service.Sample());

        string outPath = "profile.txt";
        int outIdx = Array.IndexOf(args, "--out");
        if (outIdx >= 0 && outIdx + 1 < args.Length) outPath = args[outIdx + 1];
        File.WriteAllLines(outPath, lines);
    }

    /// <summary>
    /// Hands fans back from a shutdown path that has nowhere to report a failure, so it
    /// swallows everything. Reaches the live view model through the main window rather than a
    /// static, so a preview run — which never drives a fan — simply finds nothing to do.
    /// </summary>
    private void RevertFans()
    {
        try { (MainWindow as MainWindow)?.RevertFans(); } catch { }
    }

    private static void LogError(Exception? ex)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "statusmonitor-error.log"),
                ex?.ToString() ?? "unknown error");
        }
        catch { }
    }

    /// <summary>Headless one-shot sample used for automated verification.</summary>
    private static void RunDump(string[] args)
    {
        string outPath = "snapshot.json";
        int idx = Array.IndexOf(args, "--out");
        if (idx >= 0 && idx + 1 < args.Length) outPath = args[idx + 1];

        var settings = AppSettings.Load();
        using var service = new MonitorService(settings);
        service.Sample();
        Thread.Sleep(1000);
        var snap = service.Sample();

        File.WriteAllText(outPath,
            JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = true }));
    }
}

