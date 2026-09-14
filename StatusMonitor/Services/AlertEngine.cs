namespace StatusMonitor.Services;

public sealed record AlertSignal(string Key, string Label, double? Value, double Limit, string Unit, bool Below = false);
public sealed record AlertNotice(DateTimeOffset Time, AlertSignal Signal);

/// <summary>Requires consecutive readings; one notice per excursion with cooldown across recoveries.</summary>
public sealed class AlertEngine
{
    private sealed class State
    {
        public DateTimeOffset? Since;
        public DateTimeOffset? LastSeen;
        public DateTimeOffset? LastNotice;
        public bool Fired;
    }
    private readonly Dictionary<string, State> _states = new();
    public void Reset() => _states.Clear();

    public IReadOnlyList<AlertNotice> Evaluate(DateTimeOffset now, IEnumerable<AlertSignal> signals, int holdSeconds, int cooldownSeconds)
    {
        var notices = new List<AlertNotice>();
        var seen = new HashSet<string>();
        foreach (var signal in signals)
        {
            seen.Add(signal.Key);
            if (!_states.TryGetValue(signal.Key, out var state)) _states[signal.Key] = state = new State();
            if (state.LastSeen is DateTimeOffset last && (now - last > TimeSpan.FromSeconds(5) || now < last))
            { state.Since = null; state.Fired = false; }
            state.LastSeen = now;
            bool exceeded = signal.Value is double value && double.IsFinite(value) &&
                (signal.Below ? value <= signal.Limit : value >= signal.Limit);
            if (!exceeded) { state.Since = null; state.Fired = false; continue; }
            state.Since ??= now;
            if (state.Fired || now - state.Since.Value < TimeSpan.FromSeconds(Math.Max(1, holdSeconds))) continue;
            if (state.LastNotice is DateTimeOffset sent && now - sent < TimeSpan.FromSeconds(Math.Max(0, cooldownSeconds))) continue;
            state.Fired = true;
            state.LastNotice = now;
            notices.Add(new AlertNotice(now, signal));
        }
        foreach (var key in _states.Keys.Where(k => !seen.Contains(k)).ToArray())
        { _states[key].Since = null; _states[key].Fired = false; _states[key].LastSeen = null; }
        return notices;
    }
}
