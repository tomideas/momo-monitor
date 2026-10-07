namespace StatusMonitor.Services;

public sealed record HistoryPoint(DateTimeOffset Time, double? Value);
public sealed record HistoryFrame(DateTimeOffset Time, IReadOnlyDictionary<string, double?> Values);

/// <summary>In-memory, timestamped history. Missing samples remain gaps, not zero readings.</summary>
public sealed class TelemetryHistory
{
    private readonly Queue<HistoryFrame> _frames = new();
    public int Count => _frames.Count;

    public void Add(DateTimeOffset time, IReadOnlyDictionary<string, double?> values)
    {
        if (_frames.Count > 0 && time < _frames.Last().Time) _frames.Clear();
        _frames.Enqueue(new HistoryFrame(time, new Dictionary<string, double?>(values)));
        while (_frames.Count > 0 && (_frames.Peek().Time < time.AddSeconds(-900) || _frames.Count > 901)) _frames.Dequeue();
    }

    public HistoryPoint[] Series(string key, int seconds, DateTimeOffset now) => _frames
        .Where(f => f.Time >= now.AddSeconds(-seconds) && f.Time <= now)
        .Select(f => new HistoryPoint(f.Time, f.Values.TryGetValue(key, out var value) && value.HasValue && double.IsFinite(value.Value) ? value : null))
        .ToArray();
}
