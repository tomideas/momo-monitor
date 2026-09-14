using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using StatusMonitor.I18n;
using StatusMonitor.Settings;
using System.Linq;
using StatusMonitor.Views;
using StatusMonitor.ViewModels;
using StatusMonitor.Models;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace StatusMonitor;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly MainViewModel _vm;
    private bool _loading;
    private Button? _detailSource;

    public MainWindow(AppSettings settings)
    {
        _settings = settings;
        Theme.Apply(settings.BackgroundTheme);
        Motion.Reduced = settings.ReduceMotion;
        InitializeComponent();
        _vm = new MainViewModel(settings);
        DataContext = _vm;
        _vm.CanPersist = !_preview;
        ApplyFont();
        PopulateSettings();
        InitializeFeatures();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (DetailsOverlay.Visibility == Visibility.Visible) CloseDetails(this, new RoutedEventArgs());
            else if (SettingsOverlay.Visibility == Visibility.Visible) CloseSettings(this, new RoutedEventArgs());
        };
        SelectView(Page.Dashboard);
        RestoreWindowPlacement();
        // The HWND only exists from here on, so this is the first point DWM can tint the caption.
        SourceInitialized += (_, _) => Theme.ApplyCaption(this);
        Loaded += (_, _) =>
        {
            // WarmUpAsync may already have done this behind the splash; if so _started is
            // set and only the mini-mode hop is still outstanding.
            if (!_started)
            {
                _started = true;
                _vm.Start();
                PopulateFeatures();
                // Fans are discovered during the first sample, so this is the earliest the
                // Fans tab can know whether to show itself. Without it the tab stays hidden
                // on any path that does not go through WarmUpAsync.
                PopulateFans();
            }
            if (_preview || !_settings.StartInMiniMode || _miniLaunched) return;
            _miniLaunched = true;
            Dispatcher.BeginInvoke(new Action(ShowMiniMode));
        };
        Closing += (_, e) =>
        {
            // Before either branch: hiding to the tray cancels the close, so a save that ran
            // only on the way out would never run for anyone using the tray.
            SaveWindowPlacement();
            // The tray menu's Exit sets _forceExit, so neither branch below can trap it.
            if (!_preview && !_forceExit && _tray is not null)
            {
                if (_settings.AskOnClose)
                {
                    e.Cancel = true;
                    ShowClosePrompt();
                    return;
                }
                // Having been told not to ask, do what was chosen last time. Without RunInTray
                // the close button really ends the program.
                if (_settings.RunInTray)
                {
                    e.Cancel = true;
                    Hide();
                    _mini?.Hide();
                    return;
                }
            }
            _exiting = true;
            _vm.Stop(!_preview);
        };
    }

    /// <summary>The three top-level pages, so nothing has to reason in booleans about which is up.</summary>
    private enum Page { Dashboard, Info, Fans }

    private void ShowDashboard(object sender, RoutedEventArgs e) => SelectView(Page.Dashboard);

    private void ShowInfo(object sender, RoutedEventArgs e) => SelectView(Page.Info);

    private void ShowFans(object sender, RoutedEventArgs e) => SelectView(Page.Fans);

    private Page _page = Page.Dashboard;

    private void SelectView(Page page)
    {
        if (DashboardView is null) return;
        _page = page;
        DashboardView.Visibility = page == Page.Dashboard ? Visibility.Visible : Visibility.Collapsed;
        InfoView.Visibility = page == Page.Info ? Visibility.Visible : Visibility.Collapsed;
        FansView.Visibility = page == Page.Fans ? Visibility.Visible : Visibility.Collapsed;

        SetNav(NavDashboard, active: page == Page.Dashboard);
        SetNav(NavInfo, active: page == Page.Info);
        SetNav(NavFans, active: page == Page.Fans);

        _vm.IsInfoVisible = page == Page.Info;
        if (page == Page.Info) _vm.RefreshInfo();
        // Populate, then fill in what the fans are doing right now: waiting for the next tick
        // meant the page opened on a row of dashes for up to two seconds.
        if (page == Page.Fans) { PopulateFans(); RefreshFanStatus(); }
        AnimateIn(page switch { Page.Info => InfoView, Page.Fans => FansView, _ => DashboardView });
    }

    /// <summary>
    /// Selection is a state, not a set of colours. Assigning Background/Foreground here as local
    /// values outranked the template's hover trigger in one direction and lost to it in the
    /// other, which left the selected tab unreadable while the pointer was over it.
    /// </summary>
    private static void SetNav(Button b, bool active) => b.Tag = active ? "on" : null;

    private void PopulateSettings()
    {
        _loading = true;
        ThemeCombo.ItemsSource = Theme.Keys.Select(k => new { Id = k, Name = Loc.Instance["theme_" + k] }).ToArray();
        ThemeCombo.SelectedValue = Theme.Normalize(_settings.BackgroundTheme);
        ReduceMotionBox.IsChecked = _settings.ReduceMotion;

        LanguageCombo.ItemsSource = new[] { "English", "繁體中文" };
        LanguageCombo.SelectedIndex = _settings.Language == "en" ? 0 : 1;

        FontCombo.ItemsSource = AppFont.FontKeys.Select(k => Loc.Instance[k]).ToArray();
        int fi = Array.IndexOf(AppFont.Values, _settings.Font);
        FontCombo.SelectedIndex = fi < 0 ? 0 : fi;

        CountryCombo.ItemsSource = AppSettings.CountryPresets.Select(p => p.Label).ToArray();
        CountryCombo.SelectedItem = _settings.Country;

        CurrencyCombo.ItemsSource = AppSettings.Currencies.Select(c => c.Code).ToArray();
        CurrencyCombo.SelectedItem = _settings.CurrencyCode;

        PriceBox.Text = _settings.PricePerKwh.ToString(CultureInfo.InvariantCulture);
        CarbonBox.Text = _settings.CarbonGPerKwh.ToString(CultureInfo.InvariantCulture);
        ProcessCountBox.Text = _settings.ProcessCount.ToString(CultureInfo.InvariantCulture);

        RefreshCombo.ItemsSource = RefreshChoices
            .Select(s => new { Id = s, Name = Loc.Instance["refresh_" + s] }).ToArray();
        RefreshCombo.SelectedValue = RefreshChoices.Contains(_settings.EffectiveRefreshSeconds)
            ? _settings.EffectiveRefreshSeconds
            : 2;

        _loading = false;
    }

    /// <summary>
    /// The intervals offered. One second is what the reading naturally wants; two is the
    /// default and what HWiNFO ships; five is for leaving it running all day. Anything slower
    /// belongs to the idle interval, which applies on its own when no window is on screen.
    /// </summary>
    private static readonly int[] RefreshChoices = { 1, 2, 5 };

    private void RefreshIntervalChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || RefreshCombo.SelectedValue is not int seconds) return;
        _settings.RefreshSeconds = seconds;
        _vm.ApplyInterval();
        if (!_preview) _settings.Save();
    }

    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedValue is not string key) return;
        _settings.BackgroundTheme = key;
        Theme.Apply(key);
        SetNav(NavDashboard, DashboardView.Visibility == Visibility.Visible);
        SetNav(NavInfo, InfoView.Visibility == Visibility.Visible);
        UpdateRangeButtons();
        if (!_preview) _settings.Save();
    }

    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        PopulateSettings();
        PopulateFeatures();
        SettingsOverlay.Visibility = Visibility.Visible;
        DashboardView.IsEnabled = InfoView.IsEnabled = FansView.IsEnabled = false;
        AnimateIn(SettingsOverlay);
        LanguageCombo.Focus();
    }

    /// <summary>Opens the settings panel programmatically (used by the --render test hook).</summary>
    public void ShowSettingsPanel()
    {
        PopulateSettings();
        PopulateFeatures();
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Puts the first controllable fan into the sensor-ramp mode so the expanded form of the
    /// Fans tab can be captured (used by the --render test hook). Safe because a preview run
    /// has fan control switched off entirely — this changes what is drawn, not what is driven.
    /// </summary>
    public void PreviewFanCustom()
    {
        PopulateFans();
        if (_fanVms.Count > 0) _fanVms[0].BeginEdit();
        UpdateLayout();
    }

    /// <summary>Opens the Fans page (used by the --render test hook).</summary>
    public void ShowFansPanel() => SelectView(Page.Fans);

    /// <summary>Switches to the Info page (used by the --render test hook).</summary>
    public void ShowInfoPanel() => SelectView(Page.Info);

    private void CloseSettings(object sender, RoutedEventArgs e)
    {
        DisarmReset();
        SettingsOverlay.Visibility = Visibility.Collapsed;
        DashboardView.IsEnabled = InfoView.IsEnabled = FansView.IsEnabled = true;
    }

    private void MotionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.ReduceMotion = ReduceMotionBox.IsChecked == true;
        Motion.Reduced = _settings.ReduceMotion;
        _settings.Save();
    }

    private void AnimateIn(FrameworkElement element)
    {
        if (_settings.ReduceMotion || !SystemParameters.ClientAreaAnimation || WindowState == WindowState.Minimized) return;
        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    private void RefreshInfo_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshInfo();
        AnimateIn(InfoView);
    }

    private void OpenInfoDetails(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: InfoCard card } button) return;
        _detailSource = button;
        DetailsPanel.DataContext = card;
        CopySpecsButton.Content = Loc.Instance["copy_specs"];
        DetailsOverlay.Visibility = Visibility.Visible;
        DashboardView.IsEnabled = InfoView.IsEnabled = false;
        AnimateIn(DetailsPanel);
        CloseDetailsButton.Focus();
    }

    private void CloseDetails(object sender, RoutedEventArgs e)
    {
        DetailsOverlay.Visibility = Visibility.Collapsed;
        DashboardView.IsEnabled = InfoView.IsEnabled = true;
        _detailSource?.Focus();
    }

    private void CopySpecs(object sender, RoutedEventArgs e)
    {
        if (DetailsPanel.DataContext is not InfoCard card) return;
        Clipboard.SetText(card.Title + Environment.NewLine + string.Join(Environment.NewLine, card.Items.Select(i => $"{i.Label}: {i.Value}")));
        CopySpecsButton.Content = Loc.Instance["copied"];
    }

    private void ToggleLanguage(object sender, RoutedEventArgs e)
    {
        _settings.Language = _settings.Language == "zh" ? "en" : "zh";
        _settings.Save();
        Loc.Instance.SetLanguage(_settings.Language);
        _vm.RefreshInfo();
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Language = LanguageCombo.SelectedIndex == 0 ? "en" : "zh";
        _settings.Save();
        Loc.Instance.SetLanguage(_settings.Language);
        ApplyFont();
        _vm.RefreshInfo();
    }

    private void FontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || FontCombo.SelectedIndex < 0) return;
        _settings.Font = AppFont.Values[FontCombo.SelectedIndex];
        _settings.Save();
        ApplyFont();
    }

    private void ApplyFont()
    {
        var family = AppFont.Resolve(_settings.Font);
        FontFamily = family;
        MetricBar.SetDefaultFont(family);
    }

    private void CountryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (CountryCombo.SelectedItem is not string label) return;

        _settings.ApplyCountry(label);
        _loading = true;
        PriceBox.Text = _settings.PricePerKwh.ToString(CultureInfo.InvariantCulture);
        CurrencyCombo.SelectedItem = _settings.CurrencyCode;
        CarbonBox.Text = _settings.CarbonGPerKwh.ToString(CultureInfo.InvariantCulture);
        _loading = false;
        _settings.Save();
    }

    private void CurrencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (CurrencyCombo.SelectedItem is string code)
        {
            _settings.CurrencyCode = code;
            _settings.Save();
            RefreshCountrySelection();
        }
    }

    private void PriceBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (double.TryParse(PriceBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= 0)
        {
            _settings.PricePerKwh = v;
            _settings.Save();
            RefreshCountrySelection();
        }
    }

    private void CarbonBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (double.TryParse(CarbonBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= 0)
        {
            _settings.CarbonGPerKwh = v;
            _settings.Save();
            RefreshCountrySelection();
        }
    }

    /// <summary>Reflects the derived country (which becomes "Custom" once a value is edited).</summary>
    private void RefreshCountrySelection()
    {
        _loading = true;
        CountryCombo.SelectedItem = _settings.Country;
        _loading = false;
    }

    private void ProcessCountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (int.TryParse(ProcessCountBox.Text, out int n) && n is >= 1 and <= 100)
        {
            _settings.ProcessCount = n;
            _settings.Save();
        }
    }

    /// <summary>
    /// Two-step confirm. Reset wipes accumulated energy for good, and the button no longer
    /// shouts in alarm red, so the safeguard moves from colour to a deliberate second press.
    /// </summary>
    private bool _resetArmed;

    private void ResetTotals_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (!_resetArmed)
        {
            _resetArmed = true;
            button.Content = Loc.Instance["confirm_reset"];
            return;
        }
        _resetArmed = false;
        button.Content = Loc.Instance["reset_totals"];
        _vm.ResetTotals();
    }

    /// <summary>Closing the panel forgets a half-pressed reset, so it cannot fire days later.</summary>
    private void DisarmReset()
    {
        _resetArmed = false;
        if (ResetTotalsButton is not null) ResetTotalsButton.Content = Loc.Instance["reset_totals"];
    }
}

