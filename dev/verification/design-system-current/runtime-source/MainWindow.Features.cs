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
    private bool _restartOnExit;
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
        // The logon task names a path, and this app is one portable file that gets moved,
        // copied beside its momo-data folder, or replaced by the next build. Re-registering
        // once per start is a single background call that keeps the entry pointing at the file
        // actually being run, instead of leaving a startup item aimed at nothing.
        if (!_preview && _settings.LaunchAtLogon) Task.Run(() => StartupService.Apply(true));
        if (!_preview)
        {
            _tray = new TrayService(RestoreMainWindow, ShowMiniMode,
                () => { RestoreMainWindow(); ShowSettingsPanel(); }, ExitApplication, OpenAlertEvent);
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
            // Last, so the replacement finds the totals written and the tray icon gone rather
            // than two instances briefly owning both.
            if (_restartOnExit)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = Environment.ProcessPath ?? "", UseShellExecute = true });
                }
                catch { /* the driver is installed either way; it can be started by hand */ }
            }
        };
    }
    private void FeatureLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        PopulateFeatures();
        PopulateProcessSort();
        _vm.RefreshFeatureSettings(false);
        _vm.RefreshAlertEventLanguage();
    }
    private void ShowAlertNotification(IReadOnlyList<AlertNotice> notices) => _tray?.Notify(notices);

    private void PopulateFeatures()
    {
        _featureLoading = true;
        TrendMetricCombo.ItemsSource = MainViewModel.TrendOptions();
        TrendMetricCombo.SelectedValue = EditedSettings.TrendMetric;
        if (TrendMetricCombo.SelectedIndex < 0) TrendMetricCombo.SelectedIndex = 0;
        GpuCombo.ItemsSource = _vm.GpuOptions();
        GpuCombo.SelectedValue = EditedSettings.PreferredGpuId;
        HideIntegratedBox.IsChecked = EditedSettings.HideIntegratedGpu;
        StartMiniBox.IsChecked = EditedSettings.StartInMiniMode;
        LaunchAtLogonBox.IsChecked = EditedSettings.LaunchAtLogon;
        // The delay is the service's to state, so the sentence takes it from there rather than
        // repeating a number that would quietly go stale if it ever changed.
        StartupHint.Text = string.Format(CultureInfo.InvariantCulture, Loc.Instance["launch_hint"], StartupService.LogonDelaySeconds);
        StartupFeedback.Visibility = Visibility.Collapsed;
        // Offered only while it is missing: once the driver is there the block is an answer to
        // a question nobody is asking any more.
        DriverBlock.Visibility = SensorDriverService.IsInstalled() ? Visibility.Collapsed : Visibility.Visible;
        MiniTopmostBox.IsChecked = EditedSettings.MiniTopmost;
        RunInTrayBox.IsChecked = EditedSettings.RunInTray;
        AskOnCloseBox.IsChecked = EditedSettings.AskOnClose;
        WallModeBox.IsChecked = EditedSettings.WallPowerMode;
        PsuWattsBox.Text = EditedSettings.PsuRatedWatts.ToString(CultureInfo.InvariantCulture);
        PsuClassCombo.ItemsSource = MainViewModel.PsuClassOptions();
        PsuClassCombo.SelectedValue = EditedSettings.PsuEfficiencyClass;
        EnableAlertsBox.IsChecked = EditedSettings.AlertsEnabled;
        CpuLimitBox.Text = EditedSettings.CpuTemperatureLimit.ToString(CultureInfo.InvariantCulture);
        GpuLimitBox.Text = EditedSettings.GpuTemperatureLimit.ToString(CultureInfo.InvariantCulture);
        RamLimitBox.Text = EditedSettings.MemoryLimit.ToString(CultureInfo.InvariantCulture);
        VramLimitBox.Text = EditedSettings.VramLimit.ToString(CultureInfo.InvariantCulture);
        DiskLimitBox.Text = EditedSettings.DiskFreeLimit.ToString(CultureInfo.InvariantCulture);
        HoldBox.Text = EditedSettings.AlertHoldSeconds.ToString();
        CooldownBox.Text = EditedSettings.AlertCooldownSeconds.ToString();
        MonitoringFeedback.Text = "";
        UpdateRangeButtons();
        _featureLoading = false;
    }
    private void SaveFeatureSettings(bool resetAlerts)
    {
        if (_settingsDraft is not null) return;
        if (!_preview) _settings.Save();
        _vm.RefreshFeatureSettings(resetAlerts);
    }
    private void SelectGpu(object sender, SelectionChangedEventArgs e)
    {
        if (_featureLoading || GpuCombo.SelectedValue is not string id) return;
        EditedSettings.PreferredGpuId = id;
        if (GpuCombo.SelectedItem is FeatureOption { IsIntegrated: true })
        {
            EditedSettings.HideIntegratedGpu = false;
            HideIntegratedBox.IsChecked = false;
        }
        SaveFeatureSettings(true);
    }
    private void ChangeFeatureCheck(object sender, RoutedEventArgs e)
    {
        if (_featureLoading) return;
        bool oldAlertsEnabled = EditedSettings.AlertsEnabled;
        bool oldHideIntegratedGpu = EditedSettings.HideIntegratedGpu;
        string oldPreferredGpu = EditedSettings.PreferredGpuId;
        EditedSettings.HideIntegratedGpu = HideIntegratedBox.IsChecked == true;
        if (EditedSettings.HideIntegratedGpu && GpuCombo.SelectedItem is FeatureOption { IsIntegrated: true })
        {
            EditedSettings.PreferredGpuId = "";
            _featureLoading = true;
            GpuCombo.SelectedValue = "";
            _featureLoading = false;
        }
        EditedSettings.StartInMiniMode = StartMiniBox.IsChecked == true;
        EditedSettings.RunInTray = RunInTrayBox.IsChecked == true;
        EditedSettings.AskOnClose = AskOnCloseBox.IsChecked == true;
        EditedSettings.MiniTopmost = MiniTopmostBox.IsChecked == true;
        if (_settingsDraft is null) _mini?.ApplyTopmost();
        EditedSettings.AlertsEnabled = EnableAlertsBox.IsChecked == true;
        EditedSettings.WallPowerMode = WallModeBox.IsChecked == true;
        SaveFeatureSettings(oldAlertsEnabled != EditedSettings.AlertsEnabled ||
            oldHideIntegratedGpu != EditedSettings.HideIntegratedGpu || oldPreferredGpu != EditedSettings.PreferredGpuId);
    }


    /// <summary>
    /// The only box on this page that does not simply write to the settings file: it asks
    /// Windows to register a logon task, and Windows can say no — registering one that runs
    /// elevated requires the app to be elevated itself, which it is not on a non-administrator
    /// run. A refusal puts the box back and says why, because a ticked box that did nothing is
    /// a promise the machine will not keep.
    /// <para>
    /// Its own handler rather than the shared one, so registering never rides along with an
    /// unrelated checkbox — including during a --render verification run, which toggles the
    /// shared ones and must leave this machine exactly as it found it.
    /// </para>
    /// </summary>
    private void ChangeLaunchAtLogon(object sender, RoutedEventArgs e)
    {
        if (_featureLoading) return;
        bool wanted = LaunchAtLogonBox.IsChecked == true;
        if (_settingsDraft is not null) { _settingsDraft.LaunchAtLogon = wanted; return; }
        // A preview run configures freely and touches nothing outside the process, exactly as
        // it never takes software control of a fan.
        bool applied = _preview || StartupService.Apply(wanted);
        StartupFeedback.Visibility = applied ? Visibility.Collapsed : Visibility.Visible;
        // Back to what is actually true, not to off: a refused removal leaves the task in place,
        // and the box has to keep saying so.
        if (!applied) wanted = EditedSettings.LaunchAtLogon;
        // Setting IsChecked raises Checked/Unchecked, never Click, so this cannot re-enter.
        LaunchAtLogonBox.IsChecked = wanted;
        EditedSettings.LaunchAtLogon = wanted;
        SaveFeatureSettings(false);
    }


    /// <summary>
    /// Puts the driver question once, on a machine that cannot read its own CPU temperature.
    /// <para>
    /// Not asked on a start that goes straight to the mini panel: the question is about
    /// readings, and the answer should be given while the readings it fixes are on screen. The
    /// monitoring page carries the same offer for as long as the driver is missing, so nothing
    /// is lost by staying quiet here.
    /// </para>
    /// </summary>
    private void MaybeAskForSensorDriver()
    {
        if (_preview || _settings.SensorDriverAsked || _settings.StartInMiniMode) return;
        if (SensorDriverService.IsInstalled()) return;
        ShowSensorDriverPrompt();
    }

    internal void ShowSensorDriverPrompt()
    {
        DriverFeedback.Visibility = Visibility.Collapsed;
        DriverOverlay.Visibility = Visibility.Visible;
        SetPagesEnabled(false);
        DriverInstallButton.Focus();
    }

    private void DismissSensorDriverPrompt()
    {
        DriverOverlay.Visibility = Visibility.Collapsed;
        SetPagesEnabled(SettingsOverlay.Visibility != Visibility.Visible);
    }

    /// <summary>Either answer settles it; a program that keeps asking is not asking.</summary>
    private void RememberDriverAsked()
    {
        if (_settings.SensorDriverAsked) return;
        _settings.SensorDriverAsked = true;
        SaveFeatureSettings(false);
    }

    private void SkipSensorDriver(object sender, RoutedEventArgs e)
    {
        RememberDriverAsked();
        DismissSensorDriverPrompt();
    }

    /// <summary>
    /// Installs the driver, from the prompt or from the monitoring page, and reopens the app.
    /// <para>
    /// Reopening rather than reopening the sensor hub in place: the hub hands out sensor and
    /// control objects that the fan rows hold on to, and tearing all of that down live to pick
    /// up a driver is a great deal of risk for a second saved. The exit goes through the normal
    /// close, so the energy total is written before anything restarts.
    /// </para>
    /// </summary>
    private async void InstallSensorDriver(object sender, RoutedEventArgs e)
    {
        bool fromPrompt = ReferenceEquals(sender, DriverInstallButton);
        RememberDriverAsked();
        DriverInstallButton.IsEnabled = DriverSkipButton.IsEnabled = DriverSettingsButton.IsEnabled = false;
        if (fromPrompt) SetDriverNote(Loc.Instance["driver_installing"], alarm: false);
        else MonitoringFeedback.Text = Loc.Instance["driver_installing"];

        // A preview run answers freely and installs nothing, exactly as it never drives a fan.
        bool installed = _preview || await Task.Run(SensorDriverService.Install);

        if (installed && !_preview)
        {
            RestartApplication();
            return;
        }

        DriverInstallButton.IsEnabled = DriverSkipButton.IsEnabled = DriverSettingsButton.IsEnabled = true;
        if (installed)
        {
            DismissSensorDriverPrompt();
            return;
        }
        if (fromPrompt) SetDriverNote(Loc.Instance["driver_failed"], alarm: true);
        else MonitoringFeedback.Text = Loc.Instance["driver_failed"];
    }

    private void SetDriverNote(string text, bool alarm)
    {
        DriverFeedback.Text = text;
        DriverFeedback.Foreground = (System.Windows.Media.Brush)FindResource(alarm ? "AlarmBrush" : "MutedBrush");
        DriverFeedback.Visibility = Visibility.Visible;
    }

    /// <summary>Ends this run and starts the same executable again once it is fully down.</summary>
    private void RestartApplication()
    {
        _restartOnExit = true;
        ExitApplication();
    }

    private void SelectPsuClass(object sender, SelectionChangedEventArgs e)
    {
        if (_featureLoading || PsuClassCombo.SelectedValue is not string id) return;
        EditedSettings.PsuEfficiencyClass = id;
        SaveFeatureSettings(false);
    }

    /// <summary>
    /// A rating outside the range a desktop supply comes in is refused, and the box is put back
    /// to the stored figure rather than left holding something that was not accepted. Silently
    /// keeping a bad entry would leave the wall figure divided by an efficiency read off the
    /// wrong part of the curve, with nothing on screen to say so.
    /// </summary>
    private void ApplyPsuWatts(object sender, RoutedEventArgs e)
    {
        if (_featureLoading) return;
        if (int.TryParse(PsuWattsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int watts)
            && watts is >= 100 and <= 2000)
        {
            EditedSettings.PsuRatedWatts = watts;
            MonitoringFeedback.Text = "";
            SaveFeatureSettings(false);
            return;
        }
        if (_settingsDraft is null) PsuWattsBox.Text = EditedSettings.PsuRatedWatts.ToString(CultureInfo.InvariantCulture);
        MonitoringFeedback.Text = Loc.Instance["invalid_psu"];
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
        EditedSettings.CpuTemperatureLimit = cpu; EditedSettings.GpuTemperatureLimit = gpu; EditedSettings.MemoryLimit = ram;
        EditedSettings.VramLimit = vram; EditedSettings.DiskFreeLimit = disk; EditedSettings.AlertHoldSeconds = hold; EditedSettings.AlertCooldownSeconds = cooldown;
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
    private int _fanRevision = -1;
    /// <summary>The row whose name is being edited. Only one is open at a time: a label does not
    /// take focus, so a click on another name would otherwise leave the first box open behind it
    /// and the page would fill with boxes. Opening one commits the one already open.</summary>
    private ViewModels.FanProfileVm? _renamingFan;

    /// <summary>
    /// Builds the Fans tab from whatever this machine actually exposes. A profile is created
    /// per controllable fan on first sight and saved only once the user changes something, so
    /// opening the tab on a machine with fans does not rewrite the settings file.
    /// </summary>
    private void PopulateFans()
    {
        var targets = _vm.Fans.Targets;
        _fanRevision = _vm.Fans.Revision;
        NoFansNote.Visibility = targets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FansIntro.Visibility = targets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        // Keep the page reachable so missing hardware has an explanation and a retry path.
        NavFans.Visibility = Visibility.Visible;

        _fanVms = new List<ViewModels.FanProfileVm>();
        _renamingFan = null;
        foreach (var target in targets)
        {
            var profile = _settings.FanProfiles.FirstOrDefault(p => p.ControlId == target.Id);
            if (profile is null)
            {
                profile = new Settings.FanProfile { ControlId = target.Id };
                _settings.FanProfiles.Add(profile);
            }
            if (!_settings.FanIdentities.TryGetValue(target.Id, out var identity))
            {
                identity = new Settings.FanIdentity();
                _settings.FanIdentities[target.Id] = identity;
            }
            _fanVms.Add(new ViewModels.FanProfileVm(target, profile, identity, () => SaveFeatureSettings(false)));
        }
        ShowAllFansBox.IsChecked = _settings.ShowAllFans;
        FanList.ItemsSource = _fanVms;
        RefreshFanVisibility();
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
        SetPagesEnabled(false);
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
        SetPagesEnabled(true);
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

    /// <summary>Opens the row's name for editing. Renaming is not part of the curve panel, so
    /// an owner who only wants to label a header can do it without choosing a mode.</summary>
    private void BeginFanRename(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ViewModels.FanProfileVm row) return;
        if (!OpenFanRename(row)) return;
        // The box only becomes visible now, so it cannot be focused until layout has run.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (((FrameworkElement)sender).Parent is System.Windows.Controls.Grid grid)
                foreach (var child in grid.Children)
                    if (child is System.Windows.Controls.TextBox box)
                    {
                        box.Focus();
                        box.SelectAll();
                        break;
                    }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>
    /// Starts editing this row's name, closing any other that is open. A click on a label does
    /// not move focus, so without this the box already open would stay open behind the new one.
    /// Returns false when this row was already the one being edited.
    /// </summary>
    private bool OpenFanRename(ViewModels.FanProfileVm row)
    {
        if (ReferenceEquals(_renamingFan, row)) return false;
        _renamingFan?.CommitRename();
        _renamingFan = row;
        row.BeginRename();
        return true;
    }

    private void EndRename(ViewModels.FanProfileVm row)
    {
        if (ReferenceEquals(_renamingFan, row)) _renamingFan = null;
    }

    /// <summary>Enter commits the typed name and Escape abandons it.</summary>
    private void RenameFanKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ViewModels.FanProfileVm row) return;
        if (e.Key == System.Windows.Input.Key.Enter) { row.CommitRename(); EndRename(row); e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.Escape) { row.CancelRename(); EndRename(row); e.Handled = true; }
    }

    /// <summary>Clicking away commits the name, the way an inline field usually behaves.</summary>
    private void CommitFanRename(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ViewModels.FanProfileVm row && row.IsRenaming)
        {
            row.CommitRename();
            EndRename(row);
        }
    }

    /// <summary>Toggles whether the fans that are not turning are listed.</summary>
    private void ChangeShowAllFans(object sender, RoutedEventArgs e)
    {
        _settings.ShowAllFans = ShowAllFansBox.IsChecked == true;
        SaveFeatureSettings(false);
        RefreshFanVisibility();
    }

    private void RefreshFanVisibility()
    {
        // Retain row VMs and their drafts; filtering must not modify hardware profiles.
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(FanList.ItemsSource);
        if (view is null) return;
        // A stopped fan is hidden, so a header that is idle or empty does not crowd the page. A
        // GPU fan is the exception: 0 RPM there is the card's own zero-fan mode — a healthy card
        // sitting idle, not an empty slot — and hiding it made the page look like it could not
        // read the card at all.
        view.Filter = item => item is FanProfileVm fan &&
            (_settings.ShowAllFans || fan.Target.CurrentRpm is null or > 0 || fan.Target.Kind is "gpu" or "cpu" ||
             fan.Profile.Mode != StatusMonitor.Settings.FanMode.Auto);
        view.Refresh();
        int hidden = _fanVms.Count - view.Cast<object>().Count();
        HiddenFansNote.Text = hidden > 0 ? string.Format(CultureInfo.InvariantCulture, Loc.Instance["fans_hidden"], hidden) : "";
        HiddenFansNote.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoRunningFansNote.Visibility = view.IsEmpty && _fanVms.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshFanStatus()
    {
        // A controller can appear after startup (USB fan hubs) or be rebuilt after sleep.
        // Recreate rows only when the service reports a real topology change; ordinary RPM
        // updates preserve the open editor and its draft values.
        if (_fanRevision != _vm.Fans.Revision) PopulateFans();
        if (FansView.Visibility != Visibility.Visible) return;
        foreach (var vm in _fanVms) vm.RefreshLive();
        RefreshFanVisibility();
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
