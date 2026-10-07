using System.Windows;
using System.Windows.Controls;
using StatusMonitor.I18n;
using StatusMonitor.Services;
using StatusMonitor.ViewModels;

namespace StatusMonitor;

public partial class MainWindow
{
    internal string VerifyDashboardScrollLayout(Action captureFit)
    {
        if (!_preview) throw new InvalidOperationException("Scroll checks require --render.");
        SelectView(Page.Dashboard);
        var content = (StackPanel)DashboardView.Content;
        var stats = content.Children.OfType<ContentControl>().Single();
        var notices = content.Children.OfType<FrameworkElement>().Where(c => c != stats)
            .Select(c => (Element: c, c.Visibility)).ToArray();
        double originalHeight = Height;
        int checks = 0;
        void Check(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); checks++; }
        void Layout()
        {
            UpdateLayout();
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            UpdateLayout();
        }
        try
        {
            // Isolate the normal dashboard layout from this preview process's driver permissions.
            foreach (var notice in notices) notice.Element.Visibility = Visibility.Collapsed;
            Height = 900;
            Layout();
            var rows = VisualTree<Controls.RowUniformPanel>(stats).Single();
            var lastRow = rows.Children.OfType<FrameworkElement>().Last();
            double bottom = lastRow.TransformToAncestor(DashboardView)
                .Transform(new Point(0, lastRow.ActualHeight)).Y + DashboardView.VerticalOffset;
            double tail = DashboardView.ExtentHeight - bottom;
            Check(Math.Abs(tail - content.Margin.Bottom) < 1,
                $"Dashboard has unused scrollable footer space: tail={tail:0.0}, gutter={content.Margin.Bottom:0.0}");
            Height += bottom + content.Margin.Bottom + 1 - DashboardView.ViewportHeight;
            Layout();
            Check(DashboardView.ScrollableHeight < 1 && DashboardView.ComputedVerticalScrollBarVisibility != Visibility.Visible,
                $"Dashboard must not scroll when the last row fits: extent={DashboardView.ExtentHeight:0.0}, viewport={DashboardView.ViewportHeight:0.0}");
            captureFit();
            Height = MinHeight;
            Layout();
            Check(DashboardView.ScrollableHeight > 0 && DashboardView.ComputedVerticalScrollBarVisibility == Visibility.Visible,
                "A short dashboard must retain its usable scrollbar");
            DashboardView.ScrollToEnd();
            Layout();
            Check(lastRow.TransformToAncestor(DashboardView).Transform(new Point(0, lastRow.ActualHeight)).Y <= DashboardView.ViewportHeight + 1,
                "NET row must remain reachable at the bottom of a short dashboard");
            return $"PASS: {checks} native dashboard scroll assertions (single bottom gutter, fit without scrollbar, short-window scrolling, NET reachable).";
        }
        finally
        {
            foreach (var notice in notices) notice.Element.Visibility = notice.Visibility;
            Height = originalHeight;
            DashboardView.ScrollToTop();
            Layout();
        }
    }

    internal string VerifyBrandFooter()
    {
        if (!_preview) throw new InvalidOperationException("Brand checks require --render.");
        var previousVisibility = SettingsOverlay.Visibility;
        try
        {
        SettingsOverlay.Visibility = Visibility.Visible;
        UpdateLayout();
        string version = typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
        string credit = new System.Windows.Documents.TextRange(DesignerCredit.ContentStart, DesignerCredit.ContentEnd).Text;
        if (credit != "Designed by Tom Tam · tomideas.com · Version " + version)
            throw new InvalidOperationException("Designer footer differs from the approved copy: " + credit);
        if (DesignerWebsite.NavigateUri.AbsoluteUri != "https://tomideas.com/" || DesignerWebsite.FontWeight != FontWeights.Bold)
            throw new InvalidOperationException("Designer website URL or emphasis differs from approved copy.");
        if (DesignerCredit.ActualWidth <= 0 || DesignerCredit.ActualHeight <= 0)
            throw new InvalidOperationException("Designer footer is not laid out.");
        return "PASS: exact designer credit; assembly version; bold website link; footer laid out. " + credit;
        }
        finally { SettingsOverlay.Visibility = previousVisibility; UpdateLayout(); }
    }

    // Runs only through the explicit render verification switch, with ephemeral settings.
    internal string VerifyFeatureInteractions()
    {
        if (!_preview) throw new InvalidOperationException("Feature checks require --render.");
        int checks = 0;
        void Check(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); checks++; }
        string originalTheme = _settings.BackgroundTheme;
        PopulateSettings();
        foreach (string key in Views.Theme.Keys)
        {
            ThemeCombo.SelectedValue = key;
            UpdateLayout();
            Check(_settings.BackgroundTheme == key, "Background selector did not update preference: " + key);
            var ink = (System.Windows.Media.SolidColorBrush)FindResource("InkBrush");
            var ground = (System.Windows.Media.SolidColorBrush)FindResource("PaperBrush");
            double inkL = Luminance(ink.Color), groundL = Luminance(ground.Color);
            double ratio = (Math.Max(inkL, groundL) + 0.05) / (Math.Min(inkL, groundL) + 0.05);
            // Was "ink.R > 240", a stand-in for "light text on the dark skin" that also pinned
            // the skin to near-white ink. Assert the real property instead: text runs the right
            // way against its own ground, and clears AAA.
            Check(key == "volt" ? inkL > groundL : inkL < groundL, "Theme text contrast did not adapt: " + key);
            Check(ratio >= 7.0, $"Body text must clear the 7:1 AAA floor: {key} is {ratio:N2}:1");
            // The upper bound is the point. Near-black under near-white measured 17.7:1 and
            // produced halation — glyphs blooming into a halo, tiring for anyone and painful
            // with astigmatism. This stops the dark ground drifting back toward pure black.
            if (key == "volt")
                Check(ratio <= 16.0, $"Dark skin must stay out of the halation range: {ratio:N2}:1");

            // Neither end of the scale. Pure black grounds bloom light text; pure white raised
            // surfaces glare, and in this warm palette a neutral white also breaks the cast.
            var surface = (System.Windows.Media.SolidColorBrush)FindResource("CardBrush");
            Check(!(surface.Color.R == 255 && surface.Color.G == 255 && surface.Color.B == 255),
                "Raised surfaces must not be pure white: " + key);
            Check(!(ground.Color.R == 0 && ground.Color.G == 0 && ground.Color.B == 0),
                "The ground must not be pure black: " + key);
            // The step from ground to raised surface should read as elevation, not as a
            // different material. Both skins are held to the same range.
            // Both skins raise surfaces above the ground, so one formula covers both.
            double step = (Luminance(surface.Color) + 0.05) / (groundL + 0.05);
            Check(step is >= 1.05 and <= 1.25, $"Surface elevation step out of range: {key} is {step:N2}");
            var pop = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush");
            Check(pop.Color != ink.Color, "Skin must carry an accent distinct from ink: " + key);
        }
        ThemeCombo.SelectedValue = Views.Theme.Normalize(originalTheme);

        // Colour says one thing only: past the threshold. A routine reading must stay Normal,
        // so the accent never implies a problem that is not there.
        foreach (var (tone, load, what) in new[]
        {
            (_vm.CpuTone, _vm.CpuLoad, "CPU"),
            (_vm.RamTone, _vm.RamLoad, "RAM"),
            (_vm.GpuCard1?.Tone ?? Views.BarTone.Normal, _vm.GpuCard1?.Load ?? 0, "GPU 1"),
            (_vm.GpuCard2?.Tone ?? Views.BarTone.Normal, _vm.GpuCard2?.Load ?? 0, "GPU 2"),
        })
        {
            bool over = load >= ViewModels.MainViewModel.AlarmPercent;
            Check(over == (tone == Views.BarTone.Alarm),
                $"{what} at {load:0}% must report alarm only past {ViewModels.MainViewModel.AlarmPercent}%");
        }
        // Disk alarms on free space, not on the used percentage.
        Check(_vm.DiskRows.All(d => d.Tone == Views.BarTone.Normal || d.Fraction > 0.5),
            "A disk may only alarm when it is substantially full");
        // Two modes only, and both render one shared body template — that is what stops the
        // dashboard and the mini panel from drifting apart. --render --mini exercises the
        // mini side for real; here we assert the shared resource exists to be rendered.
        Check(Application.Current.TryFindResource("StatBody") is DataTemplate,
            "The shared StatBody template must resolve for both windows");

        // Navigation selection must be a state, never local brushes: local values used to
        // outrank the hover trigger one way and lose to it the other, leaving the selected tab
        // dark-on-dark while hovered. Nothing may assign Foreground or Background directly.
        // The Fans tab lives to the right of Info, and only appears once a controllable fan has
        // been found. It stayed hidden on the dashboard once because the startup path never ran
        // the discovery — the tab existed and was unreachable.
        Check(IndexInNav(NavDashboard) < IndexInNav(NavInfo) && IndexInNav(NavInfo) < IndexInNav(NavFans) &&
            IndexInNav(NavFans) < IndexInNav(NavProcess) && IndexInNav(NavProcess) < IndexInNav(NavAlerts),
            "Nav order must be Dashboard, Info, Fans, Process, Alerts");
        Check(NavFans.Visibility == Visibility.Visible,
            "Fans must remain reachable to explain unavailable hardware control");

        foreach (var nav in new[] { NavDashboard, NavInfo, NavFans, NavProcess, NavAlerts })
        {
            Check(nav.ReadLocalValue(ForegroundProperty) == DependencyProperty.UnsetValue &&
                  nav.ReadLocalValue(BackgroundProperty) == DependencyProperty.UnsetValue,
                "Nav tabs must not carry local brushes; selection is driven by Tag");
        }
        SelectView(Page.Dashboard);
        UpdateLayout();
        Check(!VisualTree<Border>(DashboardView).Contains(TrendsCard) && !VisualTree<Border>(DashboardView).Contains(ProcessesCard) &&
            !VisualTree<TextBlock>(DashboardView).Any(t => t.Text == Loc.Instance["hdr_fans"]), "Dashboard must not duplicate trends, processes or fans");
        NavProcess.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UpdateLayout();
        Check(ProcessView.IsVisible && TrendsCard.IsVisible && ProcessesCard.IsVisible && !DashboardView.IsVisible,
            $"Process tab must contain trends and table: page={ProcessView.IsVisible}, trends={TrendsCard.IsVisible}, table={ProcessesCard.IsVisible}, dashboard={DashboardView.IsVisible}");
        SelectView(Page.Dashboard);
        Check(Equals(NavDashboard.Foreground, FindResource("InkBrush")) &&
              Equals(NavInfo.Foreground, FindResource("MutedBrush")),
            "The selected nav tab must read as ink and the other as muted");
        PopulateFeatures();
        var choices = _vm.GpuOptions();
        if (choices.Count > 1)
        {
            GpuCombo.SelectedValue = choices[^1].Id;
            Check(_settings.PreferredGpuId == choices[^1].Id, "GPU choice did not reach settings");
            Check(choices[^1].Name.StartsWith(_vm.PrimaryGpuName), "Primary card did not follow the selected GPU");
        }
        var integratedChoice = choices.FirstOrDefault(o => o.IsIntegrated);
        if (integratedChoice is not null && choices.Any(o => o.Id.Length > 0 && !o.IsIntegrated))
        {
            GpuCombo.SelectedValue = integratedChoice.Id;
            HideIntegratedBox.IsChecked = true;
            ChangeFeatureCheck(this, new RoutedEventArgs());
            Check(!_vm.Gpu2Visible && _settings.PreferredGpuId == "", "Hiding integrated GPU must remove its card and resolve conflicting preference");
            GpuCombo.SelectedValue = integratedChoice.Id;
            Check(!_settings.HideIntegratedGpu && _vm.Gpu2Visible, "Explicit integrated selection must make it visible again");
        }
        TrendMetricCombo.SelectedValue = "gpu";
        Range900.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(_vm.TrendSeconds == 900 && _settings.TrendMetric == "gpu", "Trend controls did not update selected range/metric");
        Check(_vm.TrendPoints.Length > 0, "Live sample history is empty");
        int oldHold = _settings.AlertHoldSeconds;
        HoldBox.Text = "0";
        ApplyAlertSettings(this, new RoutedEventArgs());
        Check(_settings.AlertHoldSeconds == oldHold && MonitoringFeedback.Text == Loc.Instance["invalid_alerts"], "Invalid alert settings were accepted");
        HoldBox.Text = "23";
        ApplyAlertSettings(this, new RoutedEventArgs());
        Check(_settings.AlertHoldSeconds == 23 && MonitoringFeedback.Text == Loc.Instance["saved"], "Valid alert settings were not applied");
        ShowMiniMode();
        Check(_mini is not null && _mini.IsVisible && !IsVisible && ReferenceEquals(_mini.DataContext, DataContext), "Mini must share telemetry and hide the main window");
        _mini!.UpdateLayout();
        // "Always on top" moved from the panel into Settings, so the assertion follows it:
        // the checkbox must reach a panel that is already open, in both directions.
        Check(!_settings.MiniTopmost && !_mini.Topmost, "Mini panel must not be pinned by default");
        MiniTopmostBox.IsChecked = true;
        ChangeFeatureCheck(this, new RoutedEventArgs());
        Check(_settings.MiniTopmost && _mini.Topmost, "Settings did not pin the open mini panel");
        MiniTopmostBox.IsChecked = false;
        ChangeFeatureCheck(this, new RoutedEventArgs());
        Check(!_settings.MiniTopmost && !_mini.Topmost, "Settings did not unpin the open mini panel");
        // Invoking it would end the run, so this only guards against the button going missing.
        Check(_mini.FindName("MiniCloseButton") is Button, "Mini panel must offer a close button");
        RestoreMainWindow();
        Check(IsVisible && !_mini.IsVisible, "Restoring dashboard did not hide mini");

        // ---- fan modes ----
        // Only meaningful on a machine that exposes a controllable fan, so it is conditional;
        // the curve maths itself is covered unconditionally in the FeatureChecks project.
        _settings.ShowAllFans = true;
        ShowFansPanel();
        UpdateLayout();
        if (_fanVms.Count > 0)
        {
            var fan = _fanVms[0];
            Check(fan.IsAuto, "A fan must start on Auto");

            // Two choices on the page, and the detail opens under the row that owns it. The
            // panel edits a copy: nothing reaches the fan until Apply, so a half-typed
            // temperature never drives anything and an open panel left alone changes nothing.
            fan.BeginEdit();
            Check(fan.IsSensor && !fan.IsAuto && fan.IsEditing,
                "Custom must open the panel on the temperature ramp, not a fixed speed");
            fan.IsConstant = true;
            Check(fan.IsConstant && !fan.IsSensor, "A fixed speed and the ramp must be exclusive");
            fan.IsSensor = true;
            fan.MaxTemperature = "999";
            Check(fan.MaxTemperature == "120",
                "An out-of-range temperature must clamp and show the clamped value back");
            fan.StartTemperature = "55";
            fan.MaxTemperature = "80";
            Check(fan.Profile.Mode == Settings.FanMode.Auto && fan.Profile.MaxTemperatureC != 80,
                "An unapplied panel must not have reached the fan");
            Check(fan.IsDirty, "An edited panel must report that it holds something unapplied");
            fan.Apply();
            Check(fan.Profile.Mode == Settings.FanMode.Sensor && fan.Profile.StartTemperatureC == 55 &&
                  fan.Profile.MaxTemperatureC == 80, "Apply must hand the panel's values to the fan");
            Check(!fan.IsDirty, "Apply must leave nothing outstanding");
            // Applying puts the panel away: left open under every configured fan, a list of six
            // becomes a wall of controls.
            Check(!fan.IsEditing && fan.IsCustom,
                "Apply must close the panel while leaving the fan on Custom");
            Check(fan.SubText.Contains("55") && fan.SubText.Contains("80"),
                "With the panel closed, the row itself must state what the fan was told");
            // And the chip has to open it again, even though it is already the lit one.
            fan.BeginEdit();
            Check(fan.IsEditing, "Custom must re-open the panel on a fan that is already custom");

            // The panel is the row's own, so it is there exactly when that row is on Custom.
            UpdateLayout();
            UpdateLayout();
            var panels = VisualTree<System.Windows.Controls.Grid>(FanList)
                .Where(g => g.Name == "CustomPanel").ToList();
            Check(panels.Count == _fanVms.Count && panels.Count(g => g.IsVisible) == 1 &&
                  panels.Single(g => g.IsVisible).DataContext == fan,
                "The custom panel must open under its own row and no other");

            // And it must be the same height in either mode. It opens under the row you are
            // working on, so a block that grows or shrinks as you choose moves the rest of the
            // list out from under the pointer.
            var panel = panels.Single(g => g.IsVisible);
            Check(VisualTree<RadioButton>(panel).Count() == 2,
                "The custom panel must offer only temperature or constant speed, with no source selector");
            fan.IsSensor = true;
            UpdateLayout();
            double rampHeight = panel.ActualHeight;
            fan.IsConstant = true;
            UpdateLayout();
            Check(Math.Abs(panel.ActualHeight - rampHeight) < 0.5,
                $"The custom panel must be the same height in either mode: {rampHeight:N1} vs {panel.ActualHeight:N1}");
            fan.IsSensor = true;
            UpdateLayout();

            double keepWidth = Width, keepHeight = Height;
            Width = MinWidth;
            Height = MinHeight;
            UpdateLayout();
            var fields = VisualTree<System.Windows.Controls.TextBox>(FanList).Where(b => b.IsVisible).ToList();
            Check(fields.Count > 0 && fields.All(b =>
                    b.TransformToAncestor(this).Transform(new Point(b.ActualWidth, 0)).X <= ActualWidth),
                "Every field in an open custom panel must stay on screen at the smallest window size");
            Width = keepWidth;
            Height = keepHeight;
            UpdateLayout();

            // The safety property that matters: a preview run configures freely and drives nothing.
            Check(!_vm.Fans.Targets[0].SoftwareControlled,
                "A preview run must never take software control of a fan");
            // A fan follows a category, not a named sensor, so at least one must resolve.
            Check(fan.Target.CpuSensorId.Length > 0 || fan.Target.GpuSensorId.Length > 0,
                "A controllable fan must have at least one temperature category to follow");
            Check(fan.Target.Kind != "gpu" || fan.Target.GpuSensorId.Length > 0,
                "A GPU fan must resolve its own card's temperature, not fall back to the CPU");

            // ---- naming and follow-source ----
            // A board reports its headers as "Fan #N"; the owner labels them. The name is not
            // part of the curve, so it works while the fan is still on Auto, and it picks the
            // glyph: a header labelled "CPU Fan" is drawn as one. A GPU fan keeps its card's
            // glyph whatever it is called, so the name is tried on a board header.
            var named = _fanVms.FirstOrDefault(f => f.Target.Kind != "gpu");
            if (named is not null)
            {
                named.BeginRename();
                Check(named.IsRenaming, "Renaming must open the name box");
                named.RenameText = "CPU Fan";
                named.CommitRename();
                Check(!named.IsRenaming && named.Title == "CPU Fan" &&
                      _settings.FanIdentities[named.Target.Id].Name == "CPU Fan",
                    "A committed name must reach the row and the settings");
                Check(named.Icon == "cpufan",
                    $"A header named 'CPU Fan' must be drawn as a CPU fan, got {named.Icon}");
                named.BeginRename();
                named.RenameText = "";
                named.CommitRename();
                Check(_settings.FanIdentities[named.Target.Id].Name.Length == 0,
                    "Clearing the name must fall back to the hardware's own name");
            }

            // Only one name opens at a time. A click on a label does not move focus, so without
            // this rule each click would leave another box open and the page would fill with
            // them — the interaction the owner reads as "boxes appearing for no reason".
            if (_fanVms.Count >= 2)
            {
                var a = _fanVms[0];
                var b = _fanVms[1];
                OpenFanRename(a);
                Check(a.IsRenaming, "Opening a name must start editing it");
                OpenFanRename(b);
                Check(b.IsRenaming && !a.IsRenaming,
                    "Opening a second name must close the one already open");
                b.CancelRename();
                EndRename(b);
                UpdateLayout();
            }

            // The name is shown, not merely stored: a binding that hid the label would leave a
            // row with no name while every property assertion above still passed.
            UpdateLayout();
            Check(VisualTree<TextBlock>(FanList).Any(t => t.Name == "FanName" && t.IsVisible),
                "A fan row must show its name while it is not being renamed");
            fan.BeginRename();
            UpdateLayout();
            Check(VisualTree<TextBox>(FanList).Any(t => t.Name == "FanNameEdit" && t.IsVisible),
                "Renaming must show an editable name box");
            Check(!VisualTree<TextBlock>(FanList).Any(t =>
                    t.Name == "FanName" && t.IsVisible && ReferenceEquals(t.DataContext, fan)),
                "The name label must step aside while its box is open");
            fan.CancelRename();
            UpdateLayout();

            Check(Models.FanFollow.Category(fan.Target.Kind) == (fan.Target.Kind == "gpu" ? "gpu" : "cpu"),
                "A fan's temperature must follow its own hardware without a user source override");

            fan.IsAuto = true;
            Check(fan.Profile.Mode == Settings.FanMode.Auto, "Returning to Auto must clear the mode");
            UpdateLayout();
            Check(!VisualTree<System.Windows.Controls.Grid>(FanList)
                .Any(g => g.Name == "CustomPanel" && g.IsVisible),
                "Returning to Auto must close the custom panel");
        }

        // ---- closing ----
        // Hiding and quitting look identical from the title bar, and the two outcomes are not
        // comparable: one keeps the energy totals running, the other ends them. So the default
        // is to ask, and the answer is only remembered when the user says to remember it.
        Check(_settings.AskOnClose, "A fresh install must ask what closing means");
        ShowClosePrompt();
        UpdateLayout();
        Check(CloseOverlay.Visibility == Visibility.Visible && !DashboardView.IsEnabled,
            "The close prompt must come up over a disabled page");
        Check(CloseDontAskBox.IsChecked != true,
            "The close prompt must not arrive with 'do not ask again' already ticked");

        // Answering without ticking the box changes nothing: the next close asks again.
        bool trayBefore = _settings.RunInTray;
        ApplyCloseChoice(hide: true, remember: false);
        Check(_settings.AskOnClose && _settings.RunInTray == trayBefore,
            "An unticked prompt must not change what happens next time");

        // Ticking it has to store the answer as well as the silence. Storing only the silence
        // would leave the next close to whatever the tray checkbox happened to say.
        ApplyCloseChoice(hide: true, remember: true);
        Check(!_settings.AskOnClose && _settings.RunInTray,
            "Choosing to hide and not be asked again must be remembered as hiding");
        ApplyCloseChoice(hide: false, remember: true);
        Check(!_settings.AskOnClose && !_settings.RunInTray,
            "Choosing to exit and not be asked again must be remembered as exiting");
        Check(RunInTrayBox.IsChecked == false && AskOnCloseBox.IsChecked == false,
            "The settings checkboxes must follow a choice made in the prompt");

        DismissClosePrompt();
        Check(CloseOverlay.Visibility == Visibility.Collapsed && DashboardView.IsEnabled,
            "Dismissing the close prompt must hand the page back");
        _settings.AskOnClose = true;
        PopulateFeatures();

        _settings.ShowAllFans = false;
        PopulateFans();
        if (_fanVms.Count > 0)
        {
            // A stopped fan is hidden so an idle or empty header does not crowd the page — except
            // a GPU fan, whose 0 RPM is the card's own zero-fan mode and is worth seeing. A board
            // header is what the hide is tested on; a GPU fan must never disappear.
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(FanList.ItemsSource);
            _settings.ShowAllFans = false;
            var header = _fanVms.FirstOrDefault(f => f.Target.Kind != "gpu");
            if (header is not null)
            {
                double? savedRpm = header.Target.CurrentRpm;
                foreach (double? rpm in new double?[] { 0, null, 1200 })
                {
                    header.Target.CurrentRpm = rpm;
                    RefreshFanVisibility();
                    bool visible = rpm is null or > 0 || header.Target.Kind == "cpu" || header.Profile.Mode != Settings.FanMode.Auto;
                    Check(view.Contains(header) == visible,
                        "Missing readings and CPU/custom fans must remain visible for diagnosis");
                }
                header.Target.CurrentRpm = savedRpm;
            }
            var gpu = _fanVms.FirstOrDefault(f => f.Target.Kind == "gpu");
            if (gpu is not null)
            {
                double? savedRpm = gpu.Target.CurrentRpm;
                foreach (double? rpm in new double?[] { 0, null, 1200 })
                {
                    gpu.Target.CurrentRpm = rpm;
                    RefreshFanVisibility();
                    Check(view.Contains(gpu),
                        "A GPU fan must stay listed at 0 RPM — that is its zero-fan mode");
                }
                gpu.Target.CurrentRpm = savedRpm;
            }
            _settings.ShowAllFans = true;
            RefreshFanVisibility();
            Check(_fanVms.All(f => view.Contains(f)), "Show all must list every stopped channel");
            _settings.ShowAllFans = false;
            RefreshFanVisibility();
        }

        // The footer's version has to be the build's, not a number somebody remembered to edit.
        // It had already drifted once: the project moved to 0.1.1 with the window still saying
        // v0.1.0, which is the kind of wrong that a bug report is filed against.
        string assemblyVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "";
        Check(_vm.VersionText == "v" + assemblyVersion,
            $"The footer version must come from the assembly: shows {_vm.VersionText}, built {assemblyVersion}");
        VerifyBrandFooter();

        // ---- the supply, and what the headline figure claims ----
        // Every combo on this page needs an explicit ItemTemplate. A bare DisplayMemberPath does
        // not reach the selection box under this style, and the box then prints the record's
        // ToString - which is exactly what the new one did before this check existed.
        foreach (var combo in VisualTree<ComboBox>(this))
            Check(combo.ItemTemplate is not null || combo.ItemsSource is null || combo.Items.Cast<object>().All(item => item is string),
                "A combo containing objects must provide an ItemTemplate: " + combo.Name);

        // Off by default: switching it on changes what the number means, and the claim cannot be
        // made without a supply to measure the load against.
        Check(_settings.WallPowerMode == false, "Counting supply losses must be opt-in");
        string dcLabel = _vm.PowerLabel;
        _settings.WallPowerMode = true;
        PopulateFeatures();
        Check(WallModeBox.IsChecked == true, "The supply-loss switch must reflect the setting");

        // A rating outside what a desktop supply comes in is refused, and the box goes back to
        // the stored figure rather than keeping something that was not accepted.
        int storedWatts = _settings.PsuRatedWatts;
        foreach (string bad in new[] { "0", "40", "5000", "half a kilowatt", "" })
        {
            PsuWattsBox.Text = bad;
            ApplyPsuWatts(PsuWattsBox, new RoutedEventArgs());
            Check(_settings.PsuRatedWatts == storedWatts, $"A rating of '{bad}' must be refused");
            Check(PsuWattsBox.Text == storedWatts.ToString(), $"A refused rating must not be left in the box: '{bad}'");
        }
        PsuWattsBox.Text = "650";
        ApplyPsuWatts(PsuWattsBox, new RoutedEventArgs());
        Check(_settings.PsuRatedWatts == 650, "A rating inside the range must be kept");

        PsuClassCombo.SelectedValue = "gold";
        Check(_settings.PsuEfficiencyClass == "gold", "The badge selector must update the preference");
        foreach (var option in ViewModels.MainViewModel.PsuClassOptions())
            Check(Power.PowerModel.EfficiencyClasses.Contains(option.Id),
                $"The badge '{option.Id}' is offered but the curve does not know it");

        // Every supported power boundary needs its own visible label.
        Check(Loc.Instance["power_now"] != Loc.Instance["power_now_wall"],
            "What the parts draw and what the wall sees must not share a label");
        Check(new[] { "power_now", "power_now_wall", "power_cpu_gpu", "battery_discharge" }
            .Select(key => Loc.Instance[key]).Contains(dcLabel),
            "The headline label must name a supported power boundary");
        _settings.WallPowerMode = false;
        _settings.PsuRatedWatts = storedWatts;
        _settings.PsuEfficiencyClass = "bronze";
        PopulateFeatures();

        // ---- the sensor driver ----
        // The offer is worthless without the file behind it: the button would run, fail, and
        // leave the user being told to go and find a kernel driver on the internet.
        using (var embedded = typeof(Services.SensorDriverService).Assembly
                   .GetManifestResourceStream(Services.SensorDriverService.ResourceName))
        {
            Check(embedded is { Length: > 1_000_000 },
                "The signed driver installer must be embedded in the build, or the offer cannot be honoured");
        }
        // Offered exactly while it can be acted on, like the GPU wattage block above.
        Check((DriverBlock.Visibility == Visibility.Visible) != Services.SensorDriverService.IsInstalled(),
            "The driver offer must be on the page exactly while the driver is missing");

        // Asked once. Declining has to be remembered, or the next start asks again, and a
        // question repeated until it gets the answer it wants is not a question.
        bool driverAskedBefore = _settings.SensorDriverAsked;
        _settings.SensorDriverAsked = false;
        ShowSensorDriverPrompt();
        UpdateLayout();
        Check(DriverOverlay.Visibility == Visibility.Visible && !DashboardView.IsEnabled,
            "The driver prompt must come up over a disabled page");
        SkipSensorDriver(this, new RoutedEventArgs());
        Check(_settings.SensorDriverAsked && DriverOverlay.Visibility == Visibility.Collapsed && DashboardView.IsEnabled,
            "Declining the driver must be remembered and hand the page back");
        _settings.SensorDriverAsked = driverAskedBefore;

        // Every fan-row icon must resolve to a real geometry. A mistyped key draws nothing at
        // all, which looks like a layout bug rather than a missing resource.
        foreach (string key in new[] { "IcoFan", "IcoCpu", "IcoGpu", "IcoCpuFan", "IcoChassis", "IcoFlow", "IcoPump" })
            foreach (string layer in new[] { key, key + "Top" })
                Check(Application.Current.TryFindResource(layer) is System.Windows.Media.Geometry,
                    "Missing or invalid icon geometry: " + layer);

        // Six rows drawn the same way defeat the point of having a glyph at all, and two of
        // these did start life as near-copies of each other.
        var drawn = new[] { "IcoFan", "IcoCpu", "IcoGpu", "IcoCpuFan", "IcoChassis", "IcoFlow", "IcoPump" }
            .Select(k => ((System.Windows.Media.Geometry)FindResource(k)).ToString()).ToList();
        Check(drawn.Distinct().Count() == drawn.Count, "Every fan-row glyph must be distinct");

        // Each glyph must also stay inside the 24-unit box both layers share: a path that spills
        // is not clipped, it simply draws over the name beside it.
        foreach (string key in new[] { "IcoChassis", "IcoChassisTop", "IcoCpuFan", "IcoCpuFanTop",
                                       "IcoFlow", "IcoFlowTop", "IcoPump", "IcoPumpTop" })
        {
            var bounds = ((System.Windows.Media.Geometry)Application.Current.FindResource(key))
                .GetRenderBounds(new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 2));
            Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= 24 && bounds.Bottom <= 24,
                $"Icon {key} must stay within the 24-unit grid, is {bounds}");
        }

        // ---- the row has to read at arm's length ----
        // Both of these were reported by eye rather than caught by a test, so they are pinned by
        // measurement now: the icon belongs at the left edge where the eye enters the row, and
        // the speed has to outrank everything beside it instead of being a fourth fact on one
        // flat grey line.
        if (_fanVms.Count > 0)
        {
            _settings.ShowAllFans = true;
            ShowFansPanel();
            UpdateLayout();
            var icon = VisualTree<System.Windows.Controls.Viewbox>(FanList).First();
            var title = VisualTree<TextBlock>(FanList).First(t => t.Text == _fanVms[0].Title);
            string expectedTitle = string.IsNullOrWhiteSpace(_fanVms[0].Target.SensorName)
                ? _fanVms[0].Target.HardwareName : _fanVms[0].Target.SensorName;
            Check(_fanVms[0].Title == expectedTitle &&
                  _fanVms[0].HardwareText.Contains(_fanVms[0].Target.HardwareName),
                "A fan row must show the short socket name and retain hardware identity only as detail");
            Check(_fanVms[0].SubText == _fanVms[0].SettingText,
                "The collapsed row must not repeat mode, control source or hardware range");
            double iconRight = icon.TransformToAncestor(this).Transform(new Point(icon.ActualWidth, 0)).X;
            double titleLeft = title.TransformToAncestor(this).Transform(new Point(0, 0)).X;
            Check(icon.ActualWidth > 0 && iconRight <= titleLeft,
                "The hardware icon must sit at the left edge of the row, before the fan's name");

            var speed = VisualTree<TextBlock>(FanList).First(t => t.Name == "SpeedValue");
            Check(speed.Text == "—" || speed.Text.EndsWith(" RPM", StringComparison.Ordinal),
                "Measured RPM, not requested percentage, must be the row's headline");
            Check(speed.FontSize >= 18 && speed.FontWeight == FontWeights.Bold,
                "The fan speed must be the row's headline number, not another line of body text");
            Check(speed.FontSize >= title.FontSize * 1.5,
                "The fan speed must outrank the fan's name: the name is a label, the speed is the reading");
            Check(((System.Windows.Media.SolidColorBrush)speed.Foreground).Color ==
                  ((System.Windows.Media.SolidColorBrush)FindResource("InkBrush")).Color,
                "The fan speed must use full-strength ink, not the muted tone reserved for context");
        }
        _mini.Close();
        Check(IsVisible, "Closing mini should restore the main window");
        using (var tray = new TrayService(() => { }, () => { }, () => { }, () => { }))
        {
            Check(tray.IsVisible, "Tray icon was not initialized");
            // Does not emit an OS notification; delivery obeys the user's Windows notification settings.
        }
        string originalLanguage = Loc.Instance.Language;
        Loc.Instance.SetLanguage(originalLanguage == "zh" ? "en" : "zh");
        UpdateLayout();
        Check(Title == Loc.Instance["app_title"], "Localized window title failed");
        Loc.Instance.SetLanguage(originalLanguage);
        // Drill-down must target the clicked GPU without changing the GPU protected by alerts.
        string savedMetric = _settings.TrendMetric;
        string savedGpu = _settings.PreferredGpuId;
        _vm.InspectMetricCommand.Execute("ram");
        Check(ProcessView.IsVisible && _settings.TrendMetric == "ram" && _vm.ProcessSortKey == "ram",
            "RAM inspection must select usage trends and rank the complete process sample by memory");
        var diskInspect = VisualTree<Button>(DashboardView).SingleOrDefault(b => Equals(b.CommandParameter, "disk"));
        Check(diskInspect is null, "The disk summary must remain a static display");
        ProcessSortCombo.SelectedValue = "disk";
        Check(_vm.ProcessSortKey == "disk" && Equals(ProcessSortCombo.SelectedValue, "disk"),
            "Process sorting must still rank disk activity");
        if (_vm.GpuCard2 is { } secondGpu)
        {
            _vm.InspectMetricCommand.Execute(secondGpu);
            Check(_vm.TrendSource == secondGpu.Name && _settings.PreferredGpuId == savedGpu,
                "Inspecting GPU 2 must not change the primary GPU alert preference");
        }
        PreviewAlerts();
        ShowAlertsPanel();
        Check(AlertsView.IsVisible && _vm.HasAlertEvents && _vm.SelectedAlertEvent?.Id == "preview-cpu",
            "Alert events must be reachable and selectable");
        Check(_vm.AlertSummary != Loc.Instance["no_alerts"], "Adding an incident must refresh the summary immediately");
        bool oldMiniTopmost = MiniTopmostBox.IsChecked == true;
        MiniTopmostBox.IsChecked = !oldMiniTopmost;
        ChangeFeatureCheck(MiniTopmostBox, new RoutedEventArgs());
        Check(_vm.SelectedAlertEvent?.IsActive == true,
            "Changing window preferences must not interrupt an active alert or reset its cooldown");
        MiniTopmostBox.IsChecked = oldMiniTopmost;
        ChangeFeatureCheck(MiniTopmostBox, new RoutedEventArgs());
        _vm.AcknowledgeAlertEvent("preview-cpu");
        Check(_vm.SelectedAlertEvent?.IsAcknowledged == true, "Acknowledgement must persist on the selected event");
        _vm.InspectSelectedAlert();
        Check(ProcessView.IsVisible && _settings.TrendMetric == "cpu_temp",
            "An event must navigate to its associated metric");
        _settings.TrendMetric = savedMetric;
        _featureLoading = true;
        TrendMetricCombo.SelectedValue = savedMetric;
        _featureLoading = false;
        _vm.SetProcessSort("power");
        _vm.RefreshFeatureSettings(false);
        // The checks walk through the Fans page and leave it up. Put the dashboard back, or the
        // render that follows is of whatever page the last assertion happened to need.
        SelectView(Page.Dashboard);
        UpdateLayout();
        return $"PASS: {checks} UI assertions (GPU selection, range, validation, mini/restore, pin, tray lifecycle, language, theme contrast)";
    }

    /// <summary>Every descendant of a given type, for asserting on what actually rendered.</summary>
    private static IEnumerable<T> VisualTree<T>(DependencyObject root) where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in VisualTree<T>(child)) yield return deeper;
        }
    }

    /// <summary>Position of a nav button among its siblings, for asserting tab order.</summary>
    private static int IndexInNav(System.Windows.Controls.Button button) =>
        button.Parent is System.Windows.Controls.Panel panel ? panel.Children.IndexOf(button) : -1;

    /// <summary>WCAG relative luminance, so contrast assertions use the real formula.</summary>
    private static double Luminance(System.Windows.Media.Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }
}
