using System.ComponentModel;
using StatusMonitor.I18n;
using StatusMonitor.Services;
using StatusMonitor.Settings;

namespace StatusMonitor.ViewModels;

// The production AlertEvents partial is tested with a UI-free host; no WPF timer or sensors run.
public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private readonly AlertEngine _alerts = new();
    public AppSettings Settings { get; } = new();
    public bool CanPersist { get; set; }
    public bool IsIdle { get; set; }
    public string AlertSummary => RecentAlertEvents.FirstOrDefault()?.SummaryText ?? Loc.Instance["no_alerts"];
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
