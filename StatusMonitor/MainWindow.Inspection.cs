using System.Windows;
using System.Windows.Controls;
using StatusMonitor.Settings;
using StatusMonitor.I18n;

namespace StatusMonitor;

public partial class MainWindow
{
    private void InitializeInspection()
    {
        _vm.InspectionRequested += RevealInspection;
        InitializeAlertNavigation();
        DataStorageService.StatusChanged += StorageStatusChanged;
        Closed += (_, _) => DataStorageService.StatusChanged -= StorageStatusChanged;
        PopulateProcessSort();
    }

    private void PopulateProcessSort()
    {
        if (FindName("ProcessSortCombo") is not ComboBox processSort) return;
        processSort.ItemsSource = ViewModels.MainViewModel.ProcessSortOptions();
        processSort.SelectedValue = _vm.ProcessSortKey;
    }

    private void RevealInspection(string metric)
    {
        if (SettingsOverlay.Visibility == Visibility.Visible) CloseSettings(this, new RoutedEventArgs());
        SelectView(Page.Process);
        _featureLoading = true;
        TrendMetricCombo.SelectedValue = _settings.TrendMetric;
        if (FindName("ProcessSortCombo") is ComboBox processSort)
            processSort.SelectedValue = _vm.ProcessSortKey;
        _featureLoading = false;
        UpdateLayout();
        if (metric == "disk") InspectProcesses(this, new RoutedEventArgs());
        else
        {
            TrendsCard.BringIntoView();
        }
    }

    private void SelectProcessSort(object sender, SelectionChangedEventArgs e)
    {
        if (_featureLoading || sender is not ComboBox { SelectedValue: string metric }) return;
        _vm.SetProcessSort(metric);
    }

    private void InspectProcesses(object sender, RoutedEventArgs e)
    {
        SelectView(Page.Process);
        if (FindName("ProcessesCard") is FrameworkElement processesCard) processesCard.BringIntoView();
        else ProcessView.ScrollToEnd();
    }
    private void OpenMonitoring(object sender, RoutedEventArgs e)
    {
        OpenSettings(sender, e);
        MonitoringTab.IsSelected = true;
    }
    private void OpenDataInfo(object sender, RoutedEventArgs e)
    {
        OpenSettings(sender, e);
        DataInfoTab.IsSelected = true;
    }
    private void RetrySensors(object sender, RoutedEventArgs e) => _vm.RetryHardwareSensors();

    private void StorageStatusChanged(DataStorageStatus status) => Dispatcher.BeginInvoke(new Action(_vm.RefreshStorageStatus));
}
