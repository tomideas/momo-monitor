using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using StatusMonitor.I18n;
using StatusMonitor.Services;
using StatusMonitor.ViewModels;

namespace StatusMonitor;

public partial class MainWindow
{
    private bool _featureLoading = true;
    private bool _exiting;
    private bool _forceExit;
    private bool _started;
    private bool _miniLaunched;
    private readonly bool _preview = Environment.GetCommandLineArgs().Contains("--render");

    /// <summary>
    /// Takes the first sensor read before the window is shown, so the splash covers the
    /// hardware hub opening instead of an empty dashboard doing it. Sets <c>_started</c> so
    /// the Loaded handler does not sample a second time.
    /// </summary>
    public async Task WarmUpAsync()
    {
        if (_started) return;
        _started = true;
        await _vm.StartAsync();
        PopulateFeatures();
        // Fans are discovered once the hub is open, so the nav tab can only appear now.
        PopulateFans();
    }
    private TrayService? _tray;
    private MiniWindow? _mini;
    // Two modes only: the full window, and the mini panel. The old "compact" middle mode was a
    // third thing to explain that showed the same rows as mini, so it is gone.

    private void InitializeFeatures()
    {
        PopulateFeatures();
        Loc.Instance.PropertyChanged += FeatureLanguageChanged;
        if (!_preview)
        {
            _tray = new TrayService(RestoreMainWindow, ShowMiniMode, ExitApplication);
            _vm.AlertsRaised += ShowAlertNotification;
        }
        // Minimise only disappears into the tray when the user asked for that. Otherwise it
        // minimises to the taskbar like any other window — a process that keeps running with
        // no visible window is exactly what made the app look impossible to quit.
        StateChanged += (_, _) =>
        {
            if (!_preview && _settings.RunInTray && _tray is not null && WindowState == WindowState.Minimized) Hide();
            RefreshIdleState();
        };
        IsVisibleChanged += (_, _) => RefreshIdleState();
        // The Fans tab shows live readings, so it follows the sampling tick while it is open.
        _vm.PropertyChanged += (_, _) => RefreshFanStatus();
        Closed += (_, _) =>
        {
            _exiting = true;
            Loc.Instance.PropertyChanged -= FeatureLanguageChanged;
            _vm.AlertsRaised -= ShowAlertNotification;
            _mini?.Close();
            _tray?.Dispose();
        };
    }
    private void FeatureLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        PopulateFeatures();
        _vm.RefreshFeatureSettings(false);
    }
    private void ShowAlertNotification(IReadOnlyList<AlertNotice> notices) => _tray?.Notify(notices);

    private void PopulateFeatures()
    {
        _featureLoading = true;
        TrendMetricCombo.ItemsSource = MainViewModel.TrendOptions();
        TrendMetricCombo.SelectedValue = _settings.TrendMetric;
        if (TrendMetricCombo.SelectedIndex < 0) TrendMetricCombo.SelectedIndex = 0;
        GpuCombo.ItemsSource = _vm.GpuOptions();
        GpuCombo.SelectedValue = _settings.PreferredGpuId;
        HideIntegratedBox.IsChecked = _settings.HideIntegratedGpu;
        StartMiniBox.IsChecked = _settings.StartInMiniMode;
        MiniTopmostBox.IsChecked = _settings.MiniTopmost;
        RunInTrayBox.IsChecked = _settings.RunInTray;
        AskOnCloseBox.IsChecked = _settings.AskOnClose;
        EnableAlertsBox.IsChecked = _settings.AlertsEnabled;
        CpuLimitBox.Text = _settings.CpuTemperatureLimit.ToString(CultureInfo.InvariantCulture);
        GpuLimitBox.Text = _settings.GpuTemperatureLimit.ToString(CultureInfo.InvariantCulture);
        RamLimitBox.Text = _settings.MemoryLimit.ToString(CultureInfo.InvariantCulture);
        VramLimitBox.Text = _settings.VramLimit.ToString(CultureInfo.InvariantCulture);
        DiskLimitBox.Text = _settings.DiskFreeLimit.ToString(CultureInfo.InvariantCulture);
        HoldBox.Text = _settings.AlertHoldSeconds.ToString();
        CooldownBox.Text = _settings.AlertCooldownSeconds.ToString();
        MonitoringFeedback.Text = "";
        UpdateRangeButtons();
        _featureLoading = false;
    }
    private void SaveFeatureSettings(bool resetAlerts)
    {
        if (!_preview) _settings.Save();
        _vm.RefreshFeatureSettings(resetAlerts);
    }
    private void SelectGpu(object sender, SelectionChangedEventArgs e)
    {
        if (_featureLoading || GpuCombo.SelectedValue is not string id) return;
        _settings.PreferredGpuId = id;
        if (GpuCombo.SelectedItem is FeatureOption { IsIntegrated: true })
        {
            _settings.HideIntegratedGpu = false;
            HideIntegratedBox.IsChecked = false;
        }
        SaveFeatureSettings(true);
    }
    private void ChangeFeatureCheck(object sender, RoutedEventArgs e)
    {
        if (_featureLoading) return;
        _settings.HideIntegratedGpu = HideIntegratedBox.IsChecked == true;
        if (_settings.HideIntegratedGpu && GpuCombo.SelectedItem is FeatureOption { IsIntegrated: true })
        {
            _settings.PreferredGpuId = "";
            _featureLoading = true;
            GpuCombo.SelectedValue = "";
            _featureLoading = false;
        }
        _settings.StartInMiniMode = StartMiniBox.IsChecked == true;
        _settings.RunInTray = RunInTrayBox.IsChecked == true;
        _settings.AskOnClose = AskOnCloseBox.IsChecked == true;
        _settings.MiniTopmost = MiniTopmostBox.IsChecked == true;
        _mini?.ApplyTopmost();
        _settings.AlertsEnabled = EnableAlertsBox.IsChecked == true;
        SaveFeatureSettings(true);
    }
    private void SelectTrendMetric(object sender, SelectionChangedEventArgs e)
    {
        if (_featureLoading || TrendMetricCombo.SelectedValue is not string id) return;
        _settings.TrendMetric = id;
        SaveFeatureSettings(false);
    }
    private void SelectTrendRange(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out int seconds)) return;
        _settings.TrendSeconds = seconds;
        UpdateRangeButtons();
        SaveFeatureSettings(false);
    }
    private void UpdateRangeButtons()
    {
        Range60.Foreground = (System.Windows.Media.Brush)FindResource(_settings.TrendSeconds == 900 ? "InkBrush" : "OnAccentBrush");
        Range900.Foreground = (System.Windows.Media.Brush)FindResource(_settings.TrendSeconds == 900 ? "OnAccentBrush" : "InkBrush");
        Range60.Background = (System.Windows.Media.Brush)FindResource(_settings.TrendSeconds == 900 ? "CardBrush" : "AmberBrush");
        Range900.Background = (System.Windows.Media.Brush)FindResource(_settings.TrendSeconds == 900 ? "AmberBrush" : "CardBrush");
    }
    private void ApplyAlertSettings(object sender, RoutedEventArgs e)
    {
        static bool Number(TextBox box, double max, out double value) => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value >= 1 && value <= max;
        if (!Number(CpuLimitBox, 150, out double cpu) || !Number(GpuLimitBox, 150, out double gpu) ||
            !Number(RamLimitBox, 100, out double ram) || !Number(VramLimitBox, 100, out double vram) || !Number(DiskLimitBox, 100, out double disk) ||
            !int.TryParse(HoldBox.Text, out int hold) || hold is < 1 or > 600 || !int.TryParse(CooldownBox.Text, out int cooldown) || cooldown is < 0 or > 3600)
        { MonitoringFeedback.Text = Loc.Instance["invalid_alerts"]; return; }
        _settings.CpuTemperatureLimit = cpu; _settings.GpuTemperatureLimit = gpu; _settings.MemoryLimit = ram;
        _settings.VramLimit = vram; _settings.DiskFreeLimit = disk; _settings.AlertHoldSeconds = hold; _settings.AlertCooldownSeconds = cooldown;
        SaveFeatureSettings(true);
        MonitoringFeedback.Text = Loc.Instance["saved"];
    }
    /// <summary>
    /// The tray's Exit, and the only way out when RunInTray traps the close button. Goes
    /// through Close() so totals are still saved on the way down.
    /// </summary>
    public void ExitApplication()
    {
        _forceExit = true;
        Close();
    }

    /// <summary>
    /// Nothing on screen means nothing is being read, so the sensors fall back to the idle
    /// interval. Recomputed from both windows rather than set at each hide/show site: there
    /// are several of those — tray close, minimise, the mini swap — and one would eventually
    /// be missed. The energy total keeps accruing either way; only the reading rate changes.
    /// </summary>
    private void RefreshIdleState()
    {
        if (_preview) return;
        bool onScreen = (IsVisible && WindowState != WindowState.Minimized) || _mini?.IsVisible == true;
        _vm.IsIdle = !onScreen;
    }

    private void OpenMini(object sender, RoutedEventArgs e) => ShowMiniMode();
    public void ShowMiniMode()
    {
        if (_mini is null)
        {
            _mini = new MiniWindow(_vm, _settings, RestoreMainWindow, CloseFromMini, _preview);
            _mini.Closing += (_, e) => { if (!_exiting && !Application.Current.Dispatcher.HasShutdownStarted) { e.Cancel = true; RestoreMainWindow(); } };
            _mini.IsVisibleChanged += (_, _) => RefreshIdleState();
        }
        // Re-read on every show: Settings owns "always on top" now, and the panel instance
        // outlives any number of trips through the settings dialog.
        _mini.ApplyTopmost();
        Hide();
        _mini.Show();
        _mini.Activate();
    }

    /// <summary>
    /// The mini panel's close button. Routed through the main window's own Close so it means
    /// exactly what the title-bar X means — ending the program, or hiding to the tray when the
    /// user turned that on — rather than inventing a second kind of close.
    /// </summary>
    public void CloseFromMini() => Close();

    /// <summary>Hands every driven fan back to its firmware; used by the process-exit guards.</summary>
    public void RevertFans() => _vm.RevertFans();

    private List<ViewModels.FanProfileVm> _fanVms = new();

    /// <summary>
    /// Builds the Fans tab from whatever this machine actually exposes. A profile is created
    /// per controllable fan on first sight and saved only once the user changes something, so
    /// opening the tab on a machine with fans does not rewrite the settings file.
    /// </summary>
    private void PopulateFans()
    {
        var targets = _vm.Fans.Targets;
        NoFansNote.Visibility = targets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FansIntro.Visibility = targets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        // A nav tab that leads to "your machine cannot do this" is worse than no tab. The page
        // still exists and --render can reach it, so the explanation is not lost.
        NavFans.Visibility = targets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        _fanVms = new List<ViewModels.FanProfileVm>();
        foreach (var target in targets)
        {
            var profile = _settings.FanProfiles.FirstOrDefault(p => p.ControlId == target.Id);
            if (profile is null)
            {
                profile = new Settings.FanProfile { ControlId = target.Id };
                _settings.FanProfiles.Add(profile);
            }
            _fanVms.Add(new ViewModels.FanProfileVm(target, profile, () => SaveFeatureSettings(false)));
        }
        FanList.ItemsSource = _fanVms;
    }

    // ---- where the window was ----

    /// <summary>The live work areas, as plain rectangles the placement rule can reason about.</summary>
    private static IEnumerable<Models.Box> WorkAreas() =>
        System.Windows.Forms.Screen.AllScreens.Select(screen => new Models.Box(
            screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height));

    /// <summary>
    /// Puts the window back where it was left. Skipped entirely when the saved spot is no longer
    /// on any attached screen, in which case the window opens centred as it would on a first run.
    /// </summary>
    private void RestoreWindowPlacement()
    {
        if (_preview) return;
        if (_settings.WindowLeft is not { } left || _settings.WindowTop is not { } top ||
            _settings.WindowWidth is not { } width || _settings.WindowHeight is not { } height) return;

        width = Math.Max(width, MinWidth);
        height = Math.Max(height, MinHeight);
        if (!Models.WindowPlacement.IsReachable(new Models.Box(left, top, width, height), WorkAreas())) return;

        // Manual, or WPF centres the window and throws the saved position away.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        if (_settings.WindowMaximized) WindowState = WindowState.Maximized;
    }

    /// <summary>Remembers the window's size and position for the next run.</summary>
    private void SaveWindowPlacement()
    {
        if (_preview) return;
        // RestoreBounds, not Left/Top/Width/Height: a maximised window reports the whole screen
        // and a minimised one reports -32000, which would be saved and then quietly rejected on
        // the next start — the window would silently stop remembering anything.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        if (bounds.IsEmpty || double.IsNaN(bounds.Width) || bounds.Width < 1 || bounds.Height < 1) return;

        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        _settings.WindowMaximized = WindowState == WindowState.Maximized;
        _settings.Save();
    }

    /// <summary>
    /// Asks whether closing means hiding or quitting. Shown instead of the close, which is
    /// cancelled first — the window only really goes away once one of the two is chosen.
    /// </summary>
    internal void ShowClosePrompt()
    {
        CloseDontAskBox.IsChecked = false;
        CloseOverlay.Visibility = Visibility.Visible;
        DashboardView.IsEnabled = InfoView.IsEnabled = FansView.IsEnabled = false;
        CloseHideButton.Focus();
    }

    private void CloseKeepRunning(object sender, RoutedEventArgs e)
    {
        ApplyCloseChoice(hide: true, remember: CloseDontAskBox.IsChecked == true);
        DismissClosePrompt();
        Hide();
        _mini?.Hide();
    }

    private void CloseAndExit(object sender, RoutedEventArgs e)
    {
        ApplyCloseChoice(hide: false, remember: CloseDontAskBox.IsChecked == true);
        DismissClosePrompt();
        ExitApplication();
    }

    /// <summary>
    /// The answer has to be stored along with the decision to stop asking. Storing only "do not
    /// ask" would leave the next close to whatever the tray checkbox happened to say, which is
    /// not what was just chosen.
    /// </summary>
    internal void ApplyCloseChoice(bool hide, bool remember)
    {
        if (!remember) return;
        _settings.AskOnClose = false;
        _settings.RunInTray = hide;
        SaveFeatureSettings(false);
        PopulateFeatures();
    }

    internal void DismissClosePrompt()
    {
        CloseOverlay.Visibility = Visibility.Collapsed;
        DashboardView.IsEnabled = InfoView.IsEnabled = FansView.IsEnabled = true;
    }

    /// <summary>Opens the row's panel.</summary>
    private void EditFanCustom(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ViewModels.FanProfileVm row) row.BeginEdit();
    }

    /// <summary>Hands what the row's panel holds to the fan, and puts the panel away.</summary>
    private void ApplyFanCustom(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ViewModels.FanProfileVm row) return;
        row.Apply();
        SaveFeatureSettings(false);
        RefreshFanStatus();
    }

    /// <summary>Refreshes the live readings on the Fans tab while it is on screen.</summary>
    private void RefreshFanStatus()
    {
        if (FansView.Visibility != Visibility.Visible) return;
        foreach (var vm in _fanVms) vm.RefreshLive();
    }
    public MiniWindow? MiniPreview => _mini;
    private void RestoreMainWindow()
    {
        _mini?.Hide();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}

