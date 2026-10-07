using System.IO;
using System.Text.Json.Serialization;
using StatusMonitor.Settings;

namespace StatusMonitor.Services;

/// <summary>
/// Which stretch of time the accumulated figures cover.
/// </summary>
public enum EnergyPeriod
{
    Today,
    Week,
    Month,
    All,
}

public sealed record EnergyCoverageSummary(double MonitoredSeconds, double MissingSeconds,
    double MeasuredSeconds, double EstimatedSeconds, bool LegacyCoverageUnknown,
    IReadOnlyDictionary<string, double> BasisSeconds, bool MultipleMachines = false);

/// <summary>
/// Energy accumulated per calendar day, so the totals can be shown for a week or a month
/// rather than only "since the last reset".
///
/// Watt-hours retain the original days-only format; additional coverage records identify
/// measured/estimated duration and missing awake time. Carbon and cost are still derived
/// from the current tariff. Legacy energy never acquires invented monitoring duration.
/// </summary>
public sealed class EnergyHistory
{
    /// <summary>Roughly 13 months: enough for "this month" and a year-on-year glance, still tiny.</summary>
    private const int KeepDays = 400;

    private static string Path => System.IO.Path.Combine(AppSettings.DataDir, "energy-history.json");

    private readonly Dictionary<DateOnly, double> _days = new();
    private readonly Dictionary<DateOnly, StoredCoverage> _coverage = new();
    private readonly HashSet<string> _machineIds = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    private readonly bool _persist;
    private readonly string _machineId;

    public EnergyHistory(bool persist = true, string? machineId = null)
    {
        _persist = persist;
        string marker = machineId ?? (persist ? DataStorageService.CurrentMachineId() : "");
        // Only one-way hash markers may reach disk, even when a caller supplies a marker.
        _machineId = marker.Length == 64 && marker.All(Uri.IsHexDigit) ? marker.ToUpperInvariant() : "";
        if (persist) Load();
    }

    /// <summary>Adds this tick's watt-hours to the given day's bucket.</summary>
    public void Add(DateOnly day, double wh)
    {
        if (!double.IsFinite(wh) || wh <= 0) return;
        lock (_lock)
        {
            _days[day] = _days.TryGetValue(day, out double existing) ? existing + wh : wh;
            if (!_coverage.TryGetValue(day, out var coverage)) _coverage[day] = coverage = new();
            coverage.LegacyCoverageUnknown = true;
        }
    }

    /// <summary>Stores observed energy and its actual monitored/missing awake time.</summary>
    public void Add(DateOnly day, double wh, double monitoredSeconds, double missingSeconds, string basis)
    {
        if (!NonNegative(wh) || !NonNegative(monitoredSeconds) || !NonNegative(missingSeconds)) return;
        if (wh == 0 && monitoredSeconds == 0 && missingSeconds == 0) return;
        basis ??= "";
        lock (_lock)
        {
            bool legacy = _days.ContainsKey(day) && !_coverage.ContainsKey(day);
            _days[day] = _days.TryGetValue(day, out double existing) ? existing + wh : wh;
            if (!_coverage.TryGetValue(day, out var coverage)) _coverage[day] = coverage = new() { LegacyCoverageUnknown = legacy };
            coverage.MonitoredSeconds += monitoredSeconds;
            coverage.MissingSeconds += missingSeconds;
            if (monitoredSeconds > 0)
                coverage.BasisSeconds[basis] = coverage.BasisSeconds.GetValueOrDefault(basis) + monitoredSeconds;
            if (_machineId.Length > 0) _machineIds.Add(_machineId);
        }
    }

    public EnergyCoverageSummary Coverage(EnergyPeriod period, DateOnly today)
    {
        lock (_lock)
        {
            var days = _days.Keys.Where(day => InPeriod(day, period, today)).ToList();
            var basis = new Dictionary<string, double>(StringComparer.Ordinal);
            double monitored = 0, missing = 0;
            bool legacy = false;
            foreach (var day in days)
            {
                if (!_coverage.TryGetValue(day, out var coverage)) { legacy = true; continue; }
                legacy |= coverage.LegacyCoverageUnknown;
                monitored += coverage.MonitoredSeconds;
                missing += coverage.MissingSeconds;
                foreach (var entry in coverage.BasisSeconds)
                    basis[entry.Key] = basis.GetValueOrDefault(entry.Key) + entry.Value;
            }
            double measured = basis.Where(b => b.Key.Equals("measured", StringComparison.Ordinal) || b.Key.StartsWith("measured:", StringComparison.Ordinal)).Sum(b => b.Value);
            double estimated = basis.Where(b => b.Key.Equals("estimated", StringComparison.Ordinal) || b.Key.StartsWith("estimated:", StringComparison.Ordinal)).Sum(b => b.Value);
            return new(monitored, missing, measured, estimated, legacy, basis, _machineIds.Count > 1);
        }
    }

    private static bool InPeriod(DateOnly day, EnergyPeriod period, DateOnly today) => period == EnergyPeriod.All ||
        (day <= today && day >= today.AddDays(-(period switch { EnergyPeriod.Today => 1, EnergyPeriod.Week => 7, _ => 30 } - 1)));

    private static bool NonNegative(double value) => double.IsFinite(value) && value >= 0;

    /// <summary>
    /// Watt-hours over the period, counted back from <paramref name="today"/> inclusive.
    /// A week is the last 7 days and a month the last 30 — rolling windows, not calendar
    /// boundaries, so the figure never collapses to nearly nothing on the 1st of a month.
    /// </summary>
    public double Sum(EnergyPeriod period, DateOnly today)
    {
        lock (_lock)
        {
            if (period == EnergyPeriod.All) return _days.Values.Sum();
            int span = period switch { EnergyPeriod.Today => 1, EnergyPeriod.Week => 7, _ => 30 };
            var from = today.AddDays(-(span - 1));
            return _days.Where(d => d.Key >= from && d.Key <= today).Sum(d => d.Value);
        }
    }

    /// <summary>
    /// The first day with a recorded figure, or null when nothing has been recorded yet.
    /// Day-by-day history only began when this feature shipped; the older running total in
    /// totals.json has no breakdown and must not be invented into one.
    /// </summary>
    public DateOnly? FirstDay
    {
        get { lock (_lock) return _days.Count == 0 ? null : _days.Keys.Min(); }
    }

    /// <summary>True when the record does not reach back far enough to cover the period.</summary>
    public bool IsPartial(EnergyPeriod period, DateOnly today)
    {
        if (period == EnergyPeriod.All) return false;
        var first = FirstDay;
        if (first is null) return true;
        int span = period switch { EnergyPeriod.Today => 1, EnergyPeriod.Week => 7, _ => 30 };
        return first.Value > today.AddDays(-(span - 1));
    }

    public void Clear()
    {
        lock (_lock) { _days.Clear(); _coverage.Clear(); _machineIds.Clear(); }
        Save();
    }

    public void Save()
    {
        if (!_persist) return;
        Dictionary<string, double> snapshot;
        Dictionary<string, StoredCoverage> coverageSnapshot;
        List<string> machines;
        lock (_lock)
        {
            var cutoff = DateOnly.FromDateTime(DateTime.Now).AddDays(-KeepDays);
            foreach (var stale in _days.Keys.Where(k => k < cutoff).ToList()) _days.Remove(stale);
            foreach (var stale in _coverage.Keys.Where(k => !_days.ContainsKey(k)).ToList()) _coverage.Remove(stale);
            snapshot = _days.ToDictionary(d => d.Key.ToString("yyyy-MM-dd"), d => Math.Round(d.Value, 3));
            coverageSnapshot = _coverage.ToDictionary(d => d.Key.ToString("yyyy-MM-dd"), d => new StoredCoverage
            {
                MonitoredSeconds = d.Value.MonitoredSeconds, MissingSeconds = d.Value.MissingSeconds,
                LegacyCoverageUnknown = d.Value.LegacyCoverageUnknown,
                BasisSeconds = new Dictionary<string, double>(d.Value.BasisSeconds, StringComparer.Ordinal)
            });
            machines = _machineIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        }
        DataStorageService.TryWriteJson(Path, new StoredHistory { Days = snapshot, Coverage = coverageSnapshot, MachineIds = machines });
    }

    private void Load()
    {
        var stored = DataStorageService.ReadJsonWithRecovery<StoredHistory>(Path,
            data => data.Days is not null && data.Days.All(entry => DateOnly.TryParse(entry.Key, out _) && NonNegative(entry.Value)) &&
                (data.Coverage is null || data.Coverage.All(entry => DateOnly.TryParse(entry.Key, out _) && entry.Value is not null &&
                    NonNegative(entry.Value.MonitoredSeconds) && NonNegative(entry.Value.MissingSeconds) &&
                    entry.Value.BasisSeconds is not null && entry.Value.BasisSeconds.All(b => NonNegative(b.Value)))) &&
                (data.MachineIds is null || data.MachineIds.All(id => id is not null && id.Length == 64 && id.All(Uri.IsHexDigit))));
        if (stored?.Days is null) return;
        foreach (var entry in stored.Days)
            _days[DateOnly.Parse(entry.Key)] = entry.Value;
        if (stored.Coverage is not null)
            foreach (var entry in stored.Coverage)
                if (_days.ContainsKey(DateOnly.Parse(entry.Key))) _coverage[DateOnly.Parse(entry.Key)] = entry.Value;
        if (stored.MachineIds is not null) foreach (var id in stored.MachineIds) _machineIds.Add(id.ToUpperInvariant());
    }

    private sealed class StoredHistory
    {
        [JsonPropertyName("days")]
        public Dictionary<string, double>? Days { get; set; }
        [JsonPropertyName("coverage")]
        public Dictionary<string, StoredCoverage>? Coverage { get; set; }
        [JsonPropertyName("machineIds")]
        public List<string>? MachineIds { get; set; }
    }

    private sealed class StoredCoverage
    {
        public double MonitoredSeconds { get; set; }
        public double MissingSeconds { get; set; }
        public bool LegacyCoverageUnknown { get; set; }
        public Dictionary<string, double> BasisSeconds { get; set; } = new(StringComparer.Ordinal);
    }
}
