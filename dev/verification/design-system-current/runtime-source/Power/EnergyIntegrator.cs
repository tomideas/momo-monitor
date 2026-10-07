namespace StatusMonitor.Power;

public sealed record EnergySegment(DateOnly Day, double EnergyWh, double MonitoredSeconds, double MissingSeconds, string Basis);

public sealed record EnergyIntegrationResult(double EnergyWh, double MonitoredSeconds, double MissingSeconds,
    string Basis, IReadOnlyList<EnergySegment> Segments);

/// <summary>
/// Integrates consecutive readings of the same power boundary. The caller supplies a
/// monotonic clock that excludes suspend; wall time is used only to select calendar days.
/// Missing readings, source changes and long gaps are never backfilled with the new watts.
/// </summary>
public sealed class EnergyIntegrator
{
    private bool _hasBaseline;
    private double _awakeSeconds;
    private DateTimeOffset _localTime;
    private double? _watts;
    private string _basis = "";

    public void Reset()
    {
        _hasBaseline = false;
        _watts = null;
        _basis = "";
    }

    public EnergyIntegrationResult Sample(double awakeSeconds, DateTimeOffset localTime,
        double? watts, string basis, double maxGapSeconds)
    {
        basis ??= "";
        double? validWatts = watts is >= 0 && double.IsFinite(watts.Value) && basis.Length > 0 ? watts : null;
        if (!double.IsFinite(awakeSeconds) || awakeSeconds < 0)
        {
            Reset();
            return Empty(basis);
        }

        if (!_hasBaseline)
        {
            SetBaseline(awakeSeconds, localTime, validWatts, basis);
            return Empty(basis);
        }

        double seconds = awakeSeconds - _awakeSeconds;
        double wallSeconds = (localTime - _localTime).TotalSeconds;
        bool limitValid = double.IsFinite(maxGapSeconds) && maxGapSeconds > 0;
        bool continuous = seconds > 0 && limitValid && seconds <= maxGapSeconds &&
            wallSeconds > 0 && wallSeconds <= maxGapSeconds &&
            Math.Abs(wallSeconds - seconds) <= Math.Max(1, maxGapSeconds * 0.1) &&
            localTime.Offset == _localTime.Offset;
        bool integrate = continuous && _watts.HasValue && validWatts.HasValue &&
            string.Equals(_basis, basis, StringComparison.Ordinal);

        IReadOnlyList<EnergySegment> segments = Array.Empty<EnergySegment>();
        if (seconds > 0 && double.IsFinite(seconds))
        {
            if (wallSeconds > 0 && localTime.Offset == _localTime.Offset && wallSeconds <= TimeSpan.FromDays(400).TotalSeconds)
                segments = Split(_localTime, localTime, seconds, integrate ? _watts : null, integrate ? validWatts : null, basis);
            else
                // A clock/time-zone change cannot be assigned to past local days reliably.
                segments = new[] { new EnergySegment(DateOnly.FromDateTime(localTime.DateTime), 0, 0, seconds, basis) };
        }
        SetBaseline(awakeSeconds, localTime, validWatts, basis);
        return new EnergyIntegrationResult(segments.Sum(s => s.EnergyWh), segments.Sum(s => s.MonitoredSeconds),
            segments.Sum(s => s.MissingSeconds), basis, segments);
    }

    private void SetBaseline(double awakeSeconds, DateTimeOffset localTime, double? watts, string basis)
    {
        _hasBaseline = true;
        _awakeSeconds = awakeSeconds;
        _localTime = localTime;
        _watts = watts;
        _basis = basis;
    }

    private static EnergyIntegrationResult Empty(string basis) => new(0, 0, 0, basis, Array.Empty<EnergySegment>());

    private static IReadOnlyList<EnergySegment> Split(DateTimeOffset start, DateTimeOffset end,
        double awakeSeconds, double? startWatts, double? endWatts, string basis)
    {
        var segments = new List<EnergySegment>();
        double duration = (end - start).TotalSeconds;
        DateTimeOffset cursor = start;
        while (cursor < end)
        {
            var midnight = new DateTimeOffset(cursor.Date.AddDays(1), cursor.Offset);
            var next = midnight < end ? midnight : end;
            double from = (cursor - start).TotalSeconds / duration;
            double to = (next - start).TotalSeconds / duration;
            double seconds = awakeSeconds * (to - from);
            bool measured = startWatts.HasValue && endWatts.HasValue;
            double wh = 0;
            if (measured)
            {
                double first = startWatts!.Value + (endWatts!.Value - startWatts.Value) * from;
                double last = startWatts.Value + (endWatts.Value - startWatts.Value) * to;
                wh = (first + last) * 0.5 * seconds / 3600;
            }
            segments.Add(new EnergySegment(DateOnly.FromDateTime(cursor.DateTime), wh,
                measured ? seconds : 0, measured ? 0 : seconds, basis));
            cursor = next;
        }
        return segments;
    }
}
