using System.IO;
using System.Text.Json;
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

/// <summary>
/// Energy accumulated per calendar day, so the totals can be shown for a week or a month
/// rather than only "since the last reset".
///
/// Only watt-hours are stored. Carbon and cost were never accumulated separately — they are
/// derived from the current tariff — so keeping one number per day matches how the running
/// total already behaves and stays correct when the user changes the rate.
/// </summary>
public sealed class EnergyHistory
{
    /// <summary>Roughly 13 months: enough for "this month" and a year-on-year glance, still tiny.</summary>
    private const int KeepDays = 400;

    private static string Path => System.IO.Path.Combine(AppSettings.DataDir, "energy-history.json");

    private readonly Dictionary<DateOnly, double> _days = new();
    private readonly object _lock = new();

    public EnergyHistory() => Load();

    /// <summary>Adds this tick's watt-hours to the given day's bucket.</summary>
    public void Add(DateOnly day, double wh)
    {
        if (!double.IsFinite(wh) || wh <= 0) return;
        lock (_lock)
        {
            _days[day] = _days.TryGetValue(day, out double existing) ? existing + wh : wh;
        }
    }

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
        lock (_lock) _days.Clear();
        Save();
    }

    public void Save()
    {
        try
        {
            Dictionary<string, double> snapshot;
            lock (_lock)
            {
                var cutoff = DateOnly.FromDateTime(DateTime.Now).AddDays(-KeepDays);
                foreach (var stale in _days.Keys.Where(k => k < cutoff).ToList()) _days.Remove(stale);
                snapshot = _days.ToDictionary(d => d.Key.ToString("yyyy-MM-dd"), d => Math.Round(d.Value, 3));
            }
            Directory.CreateDirectory(AppSettings.DataDir);
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new { days = snapshot }));
            File.Move(tmp, Path, overwrite: true);
        }
        catch { /* history is a convenience; never break monitoring over it */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(Path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(Path));
            if (!doc.RootElement.TryGetProperty("days", out var days)) return;
            foreach (var entry in days.EnumerateObject())
            {
                if (DateOnly.TryParse(entry.Name, out var day) &&
                    entry.Value.ValueKind == JsonValueKind.Number)
                {
                    _days[day] = entry.Value.GetDouble();
                }
            }
        }
        catch { /* a corrupt file just means starting the history over */ }
    }
}
