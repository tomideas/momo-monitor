using System.IO;
using StatusMonitor.Settings;

namespace StatusMonitor.Services;

/// <summary>Seven-day incident journal. Active entries end at their last observation across restarts.</summary>
public sealed class AlertHistory
{
    public const int RetentionDays = 7;
    private readonly string _path;
    private readonly bool _persist;
    private readonly Dictionary<string, AlertIncident> _events = new();
    private DateTimeOffset _lastSave;
    private bool _dirty;

    public sealed class Journal
    {
        public int Version { get; set; } = 1;
        public List<AlertIncident> Events { get; set; } = new();
    }

    public AlertHistory(string? path = null, DateTimeOffset? now = null, bool persist = true)
    {
        _path = path ?? Path.Combine(AppSettings.DataDir, "alert-history.json");
        _persist = persist;
        var time = now ?? DateTimeOffset.UtcNow;
        var saved = persist ? DataStorageService.ReadJsonWithRecovery<Journal>(_path,
            journal => journal.Version == 1 && journal.Events is not null && journal.Events.All(IsValid)) : null;
        if (saved is not null)
            foreach (var incident in saved.Events ?? new())
            {
                _events[incident.Id] = incident.Status == AlertIncidentStatus.Active
                    ? incident with { Status = AlertIncidentStatus.Interrupted, EndReason = AlertEndReason.Restarted,
                        EndedAt = incident.LastObservedAt, CurrentValue = null }
                    : incident;
                if (incident.Status == AlertIncidentStatus.Active) _dirty = true;
            }
        Prune(time);
        Save(time);
    }

    public IReadOnlyList<AlertIncident> Events => _events.Values
        .OrderByDescending(e => e.Status == AlertIncidentStatus.Active)
        .ThenByDescending(e => e.StartedAt).ToArray();

    public void Apply(DateTimeOffset now, IEnumerable<AlertIncident> updates)
    {
        bool transition = false;
        foreach (var update in updates)
        {
            var incident = update;
            if (_events.TryGetValue(update.Id, out var old))
            {
                incident = update with { AcknowledgedAt = old.AcknowledgedAt };
                transition |= old.Status != update.Status || old.NotifiedAt != update.NotifiedAt;
            }
            else transition = true;
            _events[incident.Id] = incident;
            _dirty = true;
        }
        Prune(now);
        // Transitions survive a sudden exit; unchanged active incidents flush at most every 30 seconds.
        if (transition || now - _lastSave >= TimeSpan.FromSeconds(30)) Save(now);
    }

    public void InterruptActive(DateTimeOffset now, AlertEndReason reason)
    {
        var updates = _events.Values.Where(e => e.Status == AlertIncidentStatus.Active).Select(e => e with
        { Status = AlertIncidentStatus.Interrupted, EndReason = reason, EndedAt = e.LastObservedAt, CurrentValue = null }).ToArray();
        Apply(now, updates);
    }

    public bool Acknowledge(string id, DateTimeOffset now)
    {
        if (!_events.TryGetValue(id, out var incident)) return false;
        _events[id] = incident with { AcknowledgedAt = now };
        _dirty = true;
        Save(now);
        return true;
    }

    public bool Save(DateTimeOffset? now = null)
    {
        if (!_dirty) return true;
        if (!_persist) { _dirty = false; return true; }
        if (!DataStorageService.TryWriteJson(_path, new Journal { Events = _events.Values.OrderBy(e => e.StartedAt).ToList() })) return false;
        _dirty = false;
        _lastSave = now ?? DateTimeOffset.UtcNow;
        return true;
    }

    private void Prune(DateTimeOffset now)
    {
        var cutoff = now.AddDays(-RetentionDays);
        foreach (var id in _events.Where(pair => pair.Value.Status != AlertIncidentStatus.Active &&
            (pair.Value.EndedAt ?? pair.Value.LastObservedAt) < cutoff).Select(pair => pair.Key).ToArray())
        { _events.Remove(id); _dirty = true; }
    }

    private static bool IsValid(AlertIncident incident) =>
        !string.IsNullOrWhiteSpace(incident.Id) && !string.IsNullOrWhiteSpace(incident.SignalKey) &&
        double.IsFinite(incident.Limit) && double.IsFinite(incident.PeakValue) &&
        (!incident.CurrentValue.HasValue || double.IsFinite(incident.CurrentValue.Value)) &&
        incident.LastObservedAt >= incident.StartedAt &&
        (!incident.EndedAt.HasValue || incident.EndedAt >= incident.StartedAt) &&
        Enum.IsDefined(incident.Status) && Enum.IsDefined(incident.EndReason);
}
