using System.Text.Json.Serialization;

namespace StatusMonitor.Services;

public sealed record AlertSignal(string Key, string Label, double? Value, double Limit, string Unit,
    bool Below = false, string TrendMetric = "", string GpuId = "");
public sealed record AlertNotice(DateTimeOffset Time, AlertSignal Signal, string EventId = "");

public enum AlertIncidentStatus { Active, Recovered, Interrupted }
public enum AlertEndReason { None, Recovered, MissingReading, SamplingGap, MonitoringStopped, Restarted, SettingsChanged, AlertsDisabled }

/// <summary>A sustained incident, independent of whether notification cooldown allowed a balloon.</summary>
public sealed record AlertIncident
{
    public string Id { get; init; } = "";
    public string SignalKey { get; init; } = "";
    public string Label { get; init; } = "";
    public string TrendMetric { get; init; } = "";
    public string GpuId { get; init; } = "";
    public string Unit { get; init; } = "";
    public double Limit { get; init; }
    public bool Below { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastObservedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public double PeakValue { get; init; }
    public double? CurrentValue { get; init; }
    public DateTimeOffset? NotifiedAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public AlertIncidentStatus Status { get; init; }
    public AlertEndReason EndReason { get; init; }
    [JsonIgnore] public TimeSpan Duration => (EndedAt ?? LastObservedAt) > StartedAt
        ? (EndedAt ?? LastObservedAt) - StartedAt : TimeSpan.Zero;
}

/// <summary>Consecutive readings create an incident; each incident notifies once, with cooldown across recoveries.</summary>
public sealed class AlertEngine
{
    private sealed class State
    {
        public DateTimeOffset? LastSeen;
        public DateTimeOffset? LastNotice;
        public int LastInterval = 1;
        public AlertIncident? Incident;
        public bool Published;
        public bool Fired;
    }
    private readonly Dictionary<string, State> _states = new();
    private readonly List<AlertIncident> _updates = new();

    /// <summary>Changed incident snapshots from the most recent Evaluate/Reset call. Consume before the next call.</summary>
    public IReadOnlyList<AlertIncident> IncidentUpdates => _updates;

    public void Reset(DateTimeOffset? now = null, AlertEndReason reason = AlertEndReason.SettingsChanged)
    {
        _updates.Clear();
        foreach (var state in _states.Values)
            End(state, AlertIncidentStatus.Interrupted, reason, state.LastSeen ?? now ?? DateTimeOffset.UtcNow, null);
        _states.Clear();
    }

    /// <summary>Allows normal timer jitter and a frequency change, while a missed interval remains a discontinuity.</summary>
    public static double ContinuityLimitSeconds(int previousIntervalSeconds, int expectedIntervalSeconds) =>
        Math.Max(5, Math.Max(Math.Max(1, previousIntervalSeconds), Math.Max(1, expectedIntervalSeconds)) * 1.5 + 1);

    public IReadOnlyList<AlertNotice> Evaluate(DateTimeOffset now, IEnumerable<AlertSignal> signals,
        int holdSeconds, int cooldownSeconds, int expectedIntervalSeconds = 1)
    {
        _updates.Clear();
        var notices = new List<AlertNotice>();
        var seen = new HashSet<string>();
        foreach (var signal in signals)
        {
            seen.Add(signal.Key);
            if (!_states.TryGetValue(signal.Key, out var state)) _states[signal.Key] = state = new State();
            if (state.LastSeen is DateTimeOffset last &&
                (now < last || (now - last).TotalSeconds > ContinuityLimitSeconds(state.LastInterval, expectedIntervalSeconds)))
                End(state, AlertIncidentStatus.Interrupted, AlertEndReason.SamplingGap, last, null);
            state.LastSeen = now;
            state.LastInterval = Math.Max(1, expectedIntervalSeconds);

            bool valid = signal.Value is double value && double.IsFinite(value) && double.IsFinite(signal.Limit);
            if (!valid)
            {
                End(state, AlertIncidentStatus.Interrupted, AlertEndReason.MissingReading,
                    state.Incident?.LastObservedAt ?? now, null);
                continue;
            }
            double reading = signal.Value!.Value;
            bool exceeded = signal.Below ? reading <= signal.Limit : reading >= signal.Limit;
            if (!exceeded)
            {
                End(state, AlertIncidentStatus.Recovered, AlertEndReason.Recovered, now, reading);
                continue;
            }

            // A changed threshold/source is a new rule, not a continuation of the old incident.
            if (state.Incident is { } old && (old.Limit != signal.Limit || old.Below != signal.Below ||
                old.TrendMetric != signal.TrendMetric || old.GpuId != signal.GpuId))
                End(state, AlertIncidentStatus.Interrupted, AlertEndReason.SettingsChanged, old.LastObservedAt, null);

            state.Incident ??= new AlertIncident
            {
                Id = Guid.NewGuid().ToString("N"), SignalKey = signal.Key, Label = signal.Label,
                TrendMetric = signal.TrendMetric, GpuId = signal.GpuId, Unit = signal.Unit,
                Limit = signal.Limit, Below = signal.Below, StartedAt = now, LastObservedAt = now,
                PeakValue = reading, CurrentValue = reading, Status = AlertIncidentStatus.Active
            };
            state.Incident = state.Incident with
            {
                LastObservedAt = now, CurrentValue = reading, Label = signal.Label,
                PeakValue = signal.Below ? Math.Min(state.Incident.PeakValue, reading) : Math.Max(state.Incident.PeakValue, reading)
            };
            if (now - state.Incident.StartedAt < TimeSpan.FromSeconds(Math.Max(1, holdSeconds))) continue;
            state.Published = true;
            if (!state.Fired && !(state.LastNotice is DateTimeOffset sent &&
                now - sent < TimeSpan.FromSeconds(Math.Max(0, cooldownSeconds))))
            {
                state.Fired = true;
                state.LastNotice = now;
                state.Incident = state.Incident with { NotifiedAt = now };
                notices.Add(new AlertNotice(now, signal, state.Incident.Id));
            }
            _updates.Add(state.Incident);
        }
        foreach (var key in _states.Keys.Where(k => !seen.Contains(k)).ToArray())
        {
            var state = _states[key];
            End(state, AlertIncidentStatus.Interrupted, AlertEndReason.MissingReading,
                state.Incident?.LastObservedAt ?? now, null);
            state.LastSeen = null;
        }
        return notices;
    }

    private void End(State state, AlertIncidentStatus status, AlertEndReason reason, DateTimeOffset at, double? reading)
    {
        if (state.Incident is { } incident && state.Published)
            _updates.Add(incident with
            {
                Status = status, EndReason = reason, EndedAt = at < incident.StartedAt ? incident.StartedAt : at,
                LastObservedAt = status == AlertIncidentStatus.Recovered ? at : incident.LastObservedAt,
                CurrentValue = reading
            });
        state.Incident = null;
        state.Published = false;
        state.Fired = false;
    }
}
