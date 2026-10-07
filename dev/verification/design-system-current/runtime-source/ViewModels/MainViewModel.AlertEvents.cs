using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using StatusMonitor.I18n;
using StatusMonitor.Services;

namespace StatusMonitor.ViewModels;

public sealed record AlertEventNavigation(string EventId, string SignalKey, string TrendMetric, string GpuId);

/// <summary>Stable rows preserve selection while an active incident changes its peak and duration.</summary>
public sealed class AlertEventVm : INotifyPropertyChanged
{
    private AlertIncident _incident;
    public AlertEventVm(AlertIncident incident) => _incident = incident;
    public AlertIncident Incident => _incident;
    public string Id => _incident.Id;
    public string SignalKey => _incident.SignalKey;
    public string Label => _incident.Label;
    public string TrendMetric => _incident.TrendMetric;
    public string GpuId => _incident.GpuId;
    public bool IsActive => _incident.Status == AlertIncidentStatus.Active;
    public bool IsAcknowledged => _incident.AcknowledgedAt.HasValue;
    public bool CanShowTrend => TrendMetric is "cpu" or "cpu_temp" or "gpu" or "gpu_temp" or "ram" or "vram" or "power";
    public string StatusText => Loc.Instance[_incident.Status switch
    { AlertIncidentStatus.Active => "alert_active", AlertIncidentStatus.Recovered => "alert_recovered", _ => "alert_interrupted" }];
    public string ReasonText => _incident.EndReason is AlertEndReason.None or AlertEndReason.Recovered ? "" :
        Loc.Instance[_incident.EndReason switch
        {
            AlertEndReason.MissingReading => "alert_reason_missing", AlertEndReason.SamplingGap => "alert_reason_gap",
            AlertEndReason.Restarted => "alert_reason_restart", AlertEndReason.SettingsChanged => "alert_reason_settings",
            AlertEndReason.AlertsDisabled => "alert_reason_disabled", _ => "alert_reason_stopped"
        }];
    public string StartedText => _incident.StartedAt.LocalDateTime.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    public string EndedText => _incident.EndedAt?.LocalDateTime.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "—";
    public string PeakText => _incident.PeakValue.ToString("0.0", CultureInfo.InvariantCulture) + _incident.Unit;
    public string PeakLabel => Loc.Instance[_incident.Below ? "alert_lowest" : "alert_peak"];
    public string ThresholdText => (_incident.Below ? "≤ " : "≥ ") +
        _incident.Limit.ToString("0.0", CultureInfo.InvariantCulture) + _incident.Unit;
    public string DurationText => _incident.Duration.TotalHours >= 1
        ? $"{(int)_incident.Duration.TotalHours}:{_incident.Duration.Minutes:00}:{_incident.Duration.Seconds:00}"
        : $"{(int)_incident.Duration.TotalMinutes}:{_incident.Duration.Seconds:00}";
    public string SummaryText => $"{StartedText}  {Label} · {StatusText} · {PeakText}";
    public string DetailText => $"{Loc.Instance["alert_started"]} {StartedText}   ·   {Loc.Instance["alert_ended"]} {EndedText}\n" +
        $"{PeakLabel} {PeakText}   ·   {Loc.Instance["alert_threshold"]} {ThresholdText}   ·   {Loc.Instance["alert_duration"]} {DurationText}" +
        (ReasonText.Length == 0 ? "" : "\n" + ReasonText);
    public string AcknowledgementText => Loc.Instance[IsAcknowledged ? "alert_acknowledged" : "alert_acknowledge"];
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Update(AlertIncident incident)
    { _incident = incident; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); }
    public void RefreshLanguage() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

public sealed partial class MainViewModel
{
    private AlertHistory? _alertJournal;
    private bool _alertJournalCanPersist;
    private AlertEventVm? _selectedAlertEvent;
    public ObservableCollection<AlertEventVm> RecentAlertEvents { get; } = new();
    public AlertEventVm? SelectedAlertEvent
    {
        get => _selectedAlertEvent;
        set { _selectedAlertEvent = value; OnPropertyChanged(nameof(SelectedAlertEvent)); }
    }
    public bool HasAlertEvents => RecentAlertEvents.Count > 0;
    public event Action<AlertEventNavigation>? AlertEventNavigationRequested;

    private AlertHistory AlertJournal
    {
        get
        {
            if (_alertJournal is null || _alertJournalCanPersist != CanPersist)
            {
                var previous = _alertJournal?.Events;
                _alertJournalCanPersist = CanPersist;
                _alertJournal = new AlertHistory(persist: CanPersist);
                if (!CanPersist && previous is not null) _alertJournal.Apply(DateTimeOffset.UtcNow, previous);
            }
            return _alertJournal;
        }
    }

    /// <summary>Call for every valid sample, in place of directly invoking AlertEngine.Evaluate.</summary>
    public IReadOnlyList<AlertNotice> EvaluateAlertEvents(DateTimeOffset now, IEnumerable<AlertSignal> signals)
    {
        int interval = IsIdle ? Settings.EffectiveIdleRefreshSeconds : Settings.EffectiveRefreshSeconds;
        var notices = _alerts.Evaluate(now, signals.Select(WithNavigation).ToArray(),
            Settings.AlertHoldSeconds, Settings.AlertCooldownSeconds, interval);
        AlertJournal.Apply(now, _alerts.IncidentUpdates);
        RefreshAlertEventList();
        return notices;
    }

    /// <summary>Call before resetting rules, disabling monitoring, marking readings stale, or stopping the app.</summary>
    public void InterruptAlertEvents(DateTimeOffset now, AlertEndReason reason)
    {
        _alerts.Reset(now, reason);
        AlertJournal.Apply(now, _alerts.IncidentUpdates);
        AlertJournal.InterruptActive(now, reason);
        RefreshAlertEventList();
    }

    public bool SaveAlertEvents() => !CanPersist || (_alertJournal?.Save() ?? true);

    /// <summary>Also call when opening the event view, before any sensor data has arrived.</summary>
    public void RefreshAlertEventList()
    {
        var events = AlertJournal.Events;
        var ids = events.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        for (int i = RecentAlertEvents.Count - 1; i >= 0; i--)
            if (!ids.Contains(RecentAlertEvents[i].Id)) RecentAlertEvents.RemoveAt(i);
        for (int i = 0; i < events.Count; i++)
        {
            var incident = events[i];
            var row = RecentAlertEvents.FirstOrDefault(e => e.Id == incident.Id);
            if (row is null) { row = new AlertEventVm(incident); RecentAlertEvents.Insert(i, row); }
            else
            {
                row.Update(incident);
                int oldIndex = RecentAlertEvents.IndexOf(row);
                if (oldIndex != i) RecentAlertEvents.Move(oldIndex, i);
            }
        }
        if (SelectedAlertEvent is not null && !ids.Contains(SelectedAlertEvent.Id)) SelectedAlertEvent = null;
        OnPropertyChanged(nameof(HasAlertEvents));
        OnPropertyChanged(nameof(SelectedAlertEvent));
        OnPropertyChanged(nameof(AlertSummary));
    }

    public void AcknowledgeAlertEvent(string id)
    {
        if (AlertJournal.Acknowledge(id, DateTimeOffset.UtcNow)) RefreshAlertEventList();
    }

    /// <summary>Selection always works; unsupported metrics (disk space) need only the event detail, not a trend.</summary>
    public void InspectAlertEvent(string id)
    {
        SelectAlertEvent(id);
        InspectSelectedAlert();
    }

    public void SelectAlertEvent(string id)
    {
        RefreshAlertEventList();
        SelectedAlertEvent = RecentAlertEvents.FirstOrDefault(e => e.Id == id);
    }

    public void InspectSelectedAlert()
    {
        if (SelectedAlertEvent is { } row)
            AlertEventNavigationRequested?.Invoke(new AlertEventNavigation(row.Id, row.SignalKey, row.TrendMetric, row.GpuId));
    }

    public void PreviewAlertEvent(AlertIncident incident)
    {
        if (CanPersist) return;
        AlertJournal.Apply(DateTimeOffset.UtcNow, new[] { incident });
        RefreshAlertEventList();
    }

    public void RefreshAlertEventLanguage()
    {
        foreach (var row in RecentAlertEvents) row.RefreshLanguage();
        OnPropertyChanged(nameof(AlertSummary));
    }

    private static AlertSignal WithNavigation(AlertSignal signal)
    {
        if (!string.IsNullOrEmpty(signal.TrendMetric)) return signal;
        if (signal.Key is "cpu_temp" or "ram") return signal with { TrendMetric = signal.Key };
        if (signal.Key.EndsWith(":temp", StringComparison.Ordinal))
            return signal with { TrendMetric = "gpu_temp", GpuId = signal.Key[..^5] };
        if (signal.Key.EndsWith(":vram", StringComparison.Ordinal))
            return signal with { TrendMetric = "vram", GpuId = signal.Key[..^5] };
        return signal;
    }
}
