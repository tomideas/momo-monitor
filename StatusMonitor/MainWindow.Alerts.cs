using System.Windows;
using StatusMonitor.ViewModels;
using StatusMonitor.Services;

namespace StatusMonitor;

public partial class MainWindow
{
    private void InitializeAlertNavigation() => _vm.AlertEventNavigationRequested += navigation =>
    {
        if (navigation.TrendMetric.Length > 0) _vm.InspectMetric(navigation.TrendMetric, navigation.GpuId);
        else
        {
            if (SettingsOverlay.Visibility == Visibility.Visible) CloseSettings(this, new RoutedEventArgs());
            SelectView(Page.Info);
        }
    };

    private void OpenAlertEvent(string id)
    {
        RestoreMainWindow();
        SelectView(Page.Alerts);
        _vm.SelectAlertEvent(id);
        AlertEventList.ScrollIntoView(_vm.SelectedAlertEvent);
    }
    private void AcknowledgeAlert(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedAlertEvent is { } selected) _vm.AcknowledgeAlertEvent(selected.Id);
    }
    private void InspectSelectedAlert(object sender, RoutedEventArgs e) => _vm.InspectSelectedAlert();
    public void ShowAlertsPanel() => SelectView(Page.Alerts);
    public void PreviewAlerts()
    {
        if (!_preview) return;
        var now = DateTimeOffset.UtcNow;
        _vm.PreviewAlertEvent(new AlertIncident
        {
            Id = "preview-cpu", SignalKey = "cpu_temp", Label = "CPU " + I18n.Loc.Instance["temperature"],
            TrendMetric = "cpu_temp", Unit = "°C", Limit = 90, PeakValue = 96, CurrentValue = 93,
            StartedAt = now.AddMinutes(-3), LastObservedAt = now, Status = AlertIncidentStatus.Active
        });
        _vm.PreviewAlertEvent(new AlertIncident
        {
            Id = "preview-ram", SignalKey = "ram", Label = I18n.Loc.Instance["ram"], TrendMetric = "ram",
            Unit = "%", Limit = 90, PeakValue = 94, CurrentValue = 72,
            StartedAt = now.AddHours(-1), LastObservedAt = now.AddMinutes(-55), EndedAt = now.AddMinutes(-55),
            Status = AlertIncidentStatus.Recovered, EndReason = AlertEndReason.Recovered
        });
        _vm.SelectAlertEvent("preview-cpu");
    }
}
