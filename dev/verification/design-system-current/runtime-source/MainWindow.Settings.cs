using System.Globalization;
using System.Text.Json;
using System.Windows;
using StatusMonitor.I18n;
using StatusMonitor.Services;
using StatusMonitor.Settings;
using StatusMonitor.ViewModels;
using StatusMonitor.Views;

namespace StatusMonitor;

public partial class MainWindow
{
    private AppSettings? _settingsDraft;
    private bool _resetPending;
    private AppSettings EditedSettings => _settingsDraft ?? _settings;

    private void UpdateSettingsLayout()
    {
        bool shortWindow = ActualHeight < 480;
        var visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        SettingsBrandRow.Visibility = DesignerCredit.Visibility = visibility;
        SettingsHeader.Margin = new Thickness(0, 0, 0, shortWindow ? 6 : 12);
        SettingsFooter.Margin = new Thickness(0, shortWindow ? 8 : 18, 0, 0);
    }

    // Only dialog-owned preferences are committed. Window placement, fan profiles and
    // monitoring history can change independently while this dialog is open.
    private static readonly string[] DialogPreferences =
    {
        nameof(AppSettings.Language), nameof(AppSettings.Font), nameof(AppSettings.ReduceMotion),
        nameof(AppSettings.BackgroundTheme), nameof(AppSettings.PreferredGpuId), nameof(AppSettings.HideIntegratedGpu),
        nameof(AppSettings.MiniTopmost), nameof(AppSettings.StartInMiniMode), nameof(AppSettings.LaunchAtLogon),
        nameof(AppSettings.RunInTray), nameof(AppSettings.AskOnClose), nameof(AppSettings.GpuTdpWatts),
        nameof(AppSettings.WallPowerMode), nameof(AppSettings.PsuRatedWatts), nameof(AppSettings.PsuEfficiencyClass),
        nameof(AppSettings.AlertsEnabled), nameof(AppSettings.CpuTemperatureLimit), nameof(AppSettings.GpuTemperatureLimit),
        nameof(AppSettings.MemoryLimit), nameof(AppSettings.VramLimit), nameof(AppSettings.DiskFreeLimit),
        nameof(AppSettings.AlertHoldSeconds), nameof(AppSettings.AlertCooldownSeconds), nameof(AppSettings.PricePerKwh),
        nameof(AppSettings.CurrencyCode), nameof(AppSettings.CarbonGPerKwh), nameof(AppSettings.ProcessCount),
        nameof(AppSettings.RefreshSeconds),
    };

    private void BeginSettingsEdit()
    {
        if (_settingsDraft is not null) return;
        _settingsDraft = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!;
        DisarmReset();
        SettingsFeedback.Text = "";
        RefreshDraftGpuRows();
    }

    private void RefreshDraftGpuRows()
    {
        if (_settingsDraft is not null)
            SettingsGpuTdpRows.ItemsSource = _vm.UnratedGpus.Select(g => new GpuTdpVm(g.Name, _settingsDraft, () => { })).ToList();
    }

    private void SaveEditedSettings()
    {
        if (_settingsDraft is null && !_preview) _settings.Save();
    }

    private void CancelSettingsEdit()
    {
        if (_settingsDraft is null) return;
        _settingsDraft = null;
        SettingsGpuTdpRows.ItemsSource = _vm.UnratedGpus;
        Theme.Apply(_settings.BackgroundTheme);
        Motion.Reduced = _settings.ReduceMotion;
        Loc.Instance.SetLanguage(_settings.Language);
        ApplyFont();
        PopulateSettings();
        PopulateFeatures();
        SettingsFeedback.Text = "";
    }

    private void AcceptSettings(object sender, RoutedEventArgs e)
    {
        if (_settingsDraft is not { } draft) return;
        if (!ReadDialogValues(draft)) return;
        if (draft.LaunchAtLogon != _settings.LaunchAtLogon && !_preview && !StartupService.Apply(draft.LaunchAtLogon))
        {
            SettingsFeedback.Text = Loc.Instance["launch_failed"];
            return;
        }
        bool resetAlerts = _settings.AlertsEnabled != draft.AlertsEnabled || _settings.PreferredGpuId != draft.PreferredGpuId ||
            _settings.HideIntegratedGpu != draft.HideIntegratedGpu || _settings.CpuTemperatureLimit != draft.CpuTemperatureLimit ||
            _settings.GpuTemperatureLimit != draft.GpuTemperatureLimit || _settings.MemoryLimit != draft.MemoryLimit ||
            _settings.VramLimit != draft.VramLimit || _settings.DiskFreeLimit != draft.DiskFreeLimit ||
            _settings.AlertHoldSeconds != draft.AlertHoldSeconds || _settings.AlertCooldownSeconds != draft.AlertCooldownSeconds;
        foreach (string name in DialogPreferences)
        {
            var property = typeof(AppSettings).GetProperty(name)!;
            property.SetValue(_settings, property.GetValue(draft));
        }
        _settingsDraft = null;
        SettingsGpuTdpRows.ItemsSource = _vm.UnratedGpus;
        if (!_preview) _settings.Save();
        _mini?.ApplyTopmost();
        _vm.ApplyInterval();
        _vm.RefreshFeatureSettings(resetAlerts);
        if (_resetPending) _vm.ResetTotals();
        CloseSettings(sender, e);
    }

    private bool ReadDialogValues(AppSettings draft)
    {
        static bool Number(string text, double min, double max, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value >= min && value <= max;
        foreach (var box in VisualTree<System.Windows.Controls.TextBox>(SettingsGpuTdpRows))
        {
            if (!string.IsNullOrWhiteSpace(box.Text) && !Number(box.Text, 1, 1000, out _))
            {
                SettingsFeedback.Text = Loc.Instance["invalid_gpu_power"];
                MonitoringTab.IsSelected = true;
                box.BringIntoView();
                return false;
            }
            // Enter can activate the default OK button without moving keyboard focus.
            box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        }
        if (!Number(PriceBox.Text, 0, double.MaxValue, out double price) || !Number(CarbonBox.Text, 0, double.MaxValue, out double carbon) ||
            !int.TryParse(ProcessCountBox.Text, out int processes) || processes is < 1 or > 100 ||
            !int.TryParse(PsuWattsBox.Text, out int supply) || supply is < 100 or > 2000)
        {
            SettingsFeedback.Text = Loc.Instance["invalid_settings"];
            return false;
        }
        if (!Number(CpuLimitBox.Text, 1, 150, out double cpu) || !Number(GpuLimitBox.Text, 1, 150, out double gpu) ||
            !Number(RamLimitBox.Text, 1, 100, out double ram) || !Number(VramLimitBox.Text, 1, 100, out double vram) ||
            !Number(DiskLimitBox.Text, 1, 100, out double disk) || !int.TryParse(HoldBox.Text, out int hold) || hold is < 1 or > 600 ||
            !int.TryParse(CooldownBox.Text, out int cooldown) || cooldown is < 0 or > 3600)
        {
            SettingsFeedback.Text = Loc.Instance["invalid_alerts"];
            MonitoringTab.IsSelected = true;
            CpuLimitBox.BringIntoView();
            return false;
        }
        draft.PricePerKwh = price; draft.CarbonGPerKwh = carbon; draft.ProcessCount = processes; draft.PsuRatedWatts = supply;
        draft.CpuTemperatureLimit = cpu; draft.GpuTemperatureLimit = gpu; draft.MemoryLimit = ram;
        draft.VramLimit = vram; draft.DiskFreeLimit = disk; draft.AlertHoldSeconds = hold; draft.AlertCooldownSeconds = cooldown;
        SettingsFeedback.Text = "";
        return true;
    }

    internal string VerifySettingsInteractions()
    {
        if (!_preview) throw new InvalidOperationException("Settings checks require --render.");
        int count = 0;
        void Check(bool ok, string reason) { count++; if (!ok) throw new InvalidOperationException(reason); }
        CloseSettings(this, new RoutedEventArgs());
        var baseline = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_settings))!;
        string originalLanguage = Loc.Instance.Language;
        const string previewGpu = "Preview unrecognised GPU";
        _vm.PreviewUnratedGpu(previewGpu);
        ShowSettingsPanel();
        UpdateLayout();
        Check(SettingsTabs.Items.Count == 3 && DataInfoTab.Header is string, "Settings must have three localized tabs");
        Check(SettingsXButton.IsVisible && SettingsOkButton.IsVisible && SettingsCancelButton.IsVisible, "Settings confirmation controls missing");
        Check(VisualTree<System.Windows.Controls.ScrollViewer>(SettingsTabs).Any(s => s.IsVisible && s.Content is System.Windows.Controls.StackPanel && s.ActualHeight >= 40),
            "Short windows must retain a usable scrolling settings area");
        Check(!DashboardView.IsEnabled && !ProcessView.IsEnabled && _settingsDraft is not null, "Settings must isolate a draft and block the underlying pages");
        DataInfoTab.IsSelected = true;
        UpdateLayout();
        Check(ResetTotalsButton.IsVisible && PowerEstimateHelp.IsVisible, "Reset and power explanation must be in Data & Info");
        Check(!VisualTree<System.Windows.Controls.Button>(SettingsOverlay).Any(b => Equals(b.Content, Loc.Instance["storage_open"]) || Equals(b.Content, Loc.Instance["storage_retry"])), "Folder options must be removed");
        PriceBox.Text = "2.5";
        HoldBox.Text = "45";
        LaunchAtLogonBox.IsChecked = !baseline.LaunchAtLogon;
        ChangeLaunchAtLogon(this, new RoutedEventArgs());
        ThemeCombo.SelectedValue = Theme.Normalize(baseline.BackgroundTheme) == "volt" ? "paper" : "volt";
        LanguageCombo.SelectedIndex = baseline.Language == "en" ? 1 : 0;
        Check(_settings.PricePerKwh == baseline.PricePerKwh && _settings.LaunchAtLogon == baseline.LaunchAtLogon &&
            _settings.BackgroundTheme == baseline.BackgroundTheme && _settings.Language == baseline.Language, "Draft edits reached live settings before OK");
        var draftGpu = ((IEnumerable<GpuTdpVm>)SettingsGpuTdpRows.ItemsSource).Single(g => g.Name == previewGpu);
        draftGpu.Watts = "250";
        Check(!_settings.GpuTdpWatts.ContainsKey(previewGpu), "GPU calibration leaked out of the draft");
        ResetTotals_Click(ResetTotalsButton, new RoutedEventArgs());
        Check(_resetArmed && !_resetPending, "Reset must require a second confirmation");
        ResetTotals_Click(ResetTotalsButton, new RoutedEventArgs());
        Check(_resetPending && ResetPendingText.Visibility == Visibility.Visible, "Confirmed reset must wait for OK");
        SettingsCancelButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(SettingsOverlay.Visibility == Visibility.Collapsed && !_resetPending && _settingsDraft is null, "Cancel did not discard reset and draft");
        Check(!_settings.GpuTdpWatts.ContainsKey(previewGpu), "Cancel did not discard GPU calibration");
        Check(_settings.PricePerKwh == baseline.PricePerKwh && Loc.Instance.Language == baseline.Language &&
            Theme.Normalize(_settings.BackgroundTheme) == Theme.Normalize(baseline.BackgroundTheme), "Cancel did not restore preferences and appearance");
        ShowSettingsPanel();
        PriceBox.Text = "3.5";
        SettingsXButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(SettingsOverlay.Visibility == Visibility.Collapsed && _settings.PricePerKwh == baseline.PricePerKwh, "X must cancel settings edits");
        ShowSettingsPanel();
        PriceBox.Text = "NaN";
        SettingsOkButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(SettingsOverlay.Visibility == Visibility.Visible && SettingsFeedback.Text.Length > 0 && _settings.PricePerKwh == baseline.PricePerKwh, "Invalid values must keep the dialog open without committing");
        MonitoringTab.IsSelected = true;
        UpdateLayout();
        var gpuBox = VisualTree<System.Windows.Controls.TextBox>(SettingsGpuTdpRows).Single(b => b.DataContext is GpuTdpVm g && g.Name == previewGpu);
        gpuBox.Text = "250";
        PriceBox.Text = "2.5";
        HoldBox.Text = "45";
        double? oldLeft = _settings.WindowLeft;
        _settings.WindowLeft = 123;
        SettingsOkButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(SettingsOverlay.Visibility == Visibility.Collapsed && _settings.PricePerKwh == 2.5 && _settings.AlertHoldSeconds == 45, "OK must commit current text fields across tabs");
        Check(_settings.GpuTdpWatts.GetValueOrDefault(previewGpu) == 250, "OK must commit a calibration text box without requiring a focus change");
        Check(_settings.WindowLeft == 123 && DashboardView.IsEnabled && ProcessView.IsEnabled, "Commit must preserve unrelated runtime state and restore page input");
        foreach (string name in DialogPreferences)
        {
            var property = typeof(AppSettings).GetProperty(name)!;
            property.SetValue(_settings, property.GetValue(baseline));
        }
        _settings.WindowLeft = oldLeft;
        Loc.Instance.SetLanguage(originalLanguage);
        _vm.RefreshFeatureSettings(false);
        ShowSettingsPanel();
        SettingsTabs.SelectedIndex = 0;
        UpdateLayout();
        var okPosition = SettingsOkButton.TransformToAncestor(SettingsOverlay).Transform(new Point(0, 0));
        var creditPosition = DesignerCredit.TransformToAncestor(SettingsOverlay).Transform(new Point(0, DesignerCredit.ActualHeight));
        Check((!DesignerCredit.IsVisible || okPosition.Y >= creditPosition.Y) && okPosition.Y + SettingsOkButton.ActualHeight <= SettingsOverlay.ActualHeight,
            "OK/Cancel must stay below the credit and inside the dialog at the current window size");
        return $"PASS: {count} native settings assertions (tabs, draft isolation, Cancel/X, deferred reset/startup, validation, OK, footer layout).";
    }
}
