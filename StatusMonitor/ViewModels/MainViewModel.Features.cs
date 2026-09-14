using System.Collections.ObjectModel;
using StatusMonitor.I18n;
using StatusMonitor.Services;

namespace StatusMonitor.ViewModels;

public sealed record FeatureOption(string Id, string Name, bool IsIntegrated = false);

public sealed partial class MainViewModel
{
    private readonly TelemetryHistory _history = new();
    private readonly AlertEngine _alerts = new();
    private int _primaryGpu = -1;
    private DateTimeOffset _lastHistory = DateTimeOffset.UtcNow;
    public ObservableCollection<string> RecentAlerts { get; } = new();
    public event Action<IReadOnlyList<AlertNotice>>? AlertsRaised;
    public HistoryPoint[] TrendPoints { get; private set; } = Array.Empty<HistoryPoint>();
    public bool IsStale { get; private set; }
    public string ReadingStatus => Loc.Instance[IsStale ? "stale" : "live"];
    public string PrimaryGpuName => GpuCard1?.Name ?? Loc.Instance["no_data"];
    public string PrimaryGpuLoadText => GpuCard1?.LoadText ?? "—";
    public string PrimaryGpuMemoryText => GpuCard1?.VramText ?? "—";
    public string PrimaryGpuMemoryLabel => GpuCard1?.MemoryLabel ?? "VRAM";
    public string CpuName => _service.CpuName;
    public string TrendSource => Settings.TrendMetric switch
    {
        "gpu" or "gpu_temp" or "vram" => PrimaryGpuName,
        "cpu" or "cpu_temp" => _service.CpuName,
        "power" => Loc.Instance["power_now"],
        _ => Loc.Instance["ram"]
    };
    public string AlertSummary => RecentAlerts.FirstOrDefault() ?? Loc.Instance["no_alerts"];
    public int TrendSeconds => Settings.TrendSeconds == 900 ? 900 : 60;
    public string TrendUnit => Settings.TrendMetric switch { "power" => "W", "cpu_temp" or "gpu_temp" => "°C", _ => "%" };
    public double TrendCeiling => Settings.TrendMetric switch
    {
        "power" => Math.Max(50, Math.Ceiling(TrendPoints.Where(p => p.Value.HasValue).Select(p => p.Value!.Value).DefaultIfEmpty(0).Max() / 50) * 50),
        "cpu_temp" or "gpu_temp" => 110,
        _ => 100
    };
    public string TrendSummary
    {
        get
        {
            var values = TrendPoints.Where(p => p.Value.HasValue).Select(p => p.Value!.Value).ToArray();
            return values.Length == 0 ? Loc.Instance["trend_wait"] :
                $"{Loc.Instance["minimum"]} {values.Min():0.0}{TrendUnit}   ·   {Loc.Instance["average"]} {values.Average():0.0}{TrendUnit}   ·   {Loc.Instance["maximum"]} {values.Max():0.0}{TrendUnit}";
        }
    }

    public List<FeatureOption> GpuOptions()
    {
        var options = new List<FeatureOption> { new("", Loc.Instance["gpu_auto"]) };
        options.AddRange(Enumerable.Range(0, _snap.Gpus.Count).Select(i => new FeatureOption(GpuSelection.Key(_snap, i),
            (i < _snap.GpuNames.Count ? _snap.GpuNames[i] : "GPU") + (_snap.Gpus[i].IsIntegrated ? " · " + Loc.Instance["integrated"] : ""), _snap.Gpus[i].IsIntegrated)));
        if (!options.Any(o => o.Id == Settings.PreferredGpuId)) options.Add(new(Settings.PreferredGpuId, Loc.Instance["gpu_unavailable"]));
        return options;
    }

    public static List<FeatureOption> TrendOptions() => new()
    {
        new("cpu", "CPU %"), new("gpu", "GPU %"), new("ram", Loc.Instance["ram"] + " %"),
        new("vram", Loc.Instance["gpu_memory_usage"]), new("cpu_temp", "CPU °C"), new("gpu_temp", "GPU °C"),
        new("power", Loc.Instance["power_now"])
    };

    private void RefreshGpuCards()
    {
        var visible = GpuSelection.Visible(_snap, Settings.PreferredGpuId, Settings.HideIntegratedGpu);
        _primaryGpu = visible.Count > 0 ? visible[0] : -1;
        GpuCard1 = BuildGpuCard(_snap, _primaryGpu);
        GpuCard2 = BuildGpuCard(_snap, visible.Count > 1 ? visible[1] : -1);
    }

    public void RefreshFeatureSettings(bool resetAlerts = true)
    {
        if (resetAlerts) _alerts.Reset();
        RefreshGpuCards();
        RefreshTrend();
        OnPropertyChanged(null);
    }

    private void RefreshTrend()
    {
        string key = Settings.TrendMetric;
        if (key is "gpu" or "gpu_temp" or "vram") key = _primaryGpu >= 0 ? GpuSelection.Key(_snap, _primaryGpu) + ":" + key : "missing_gpu";
        TrendPoints = _history.Series(key, TrendSeconds, _lastHistory);
    }

    private void RecordFeatures(DateTimeOffset now)
    {
        IsStale = false;
        _lastHistory = now;
        var values = new Dictionary<string, double?>
        { ["cpu"] = _snap.Cpu.LoadPercent, ["cpu_temp"] = _snap.Cpu.TemperatureC, ["ram"] = _snap.RamLoadPercent, ["power"] = _snap.TotalWatts };
        for (int i = 0; i < _snap.Gpus.Count; i++)
        {
            var gpu = _snap.Gpus[i];
            string key = GpuSelection.Key(_snap, i);
            values[key + ":gpu"] = gpu.LoadPercent;
            values[key + ":gpu_temp"] = gpu.TemperatureC;
            values[key + ":vram"] = gpu.MemoryTotalMb is > 0 && gpu.MemoryUsedMb.HasValue ? gpu.MemoryUsedMb.Value / gpu.MemoryTotalMb.Value * 100 : null;
        }
        _history.Add(now, values);
        RefreshTrend();
        if (!Settings.AlertsEnabled) { _alerts.Reset(); return; }
        var signals = new List<AlertSignal>
        {
            new("cpu_temp", "CPU " + Loc.Instance["temperature"], _snap.Cpu.TemperatureC, Settings.CpuTemperatureLimit, "°C"),
            new("ram", Loc.Instance["ram"], _snap.RamLoadPercent, Settings.MemoryLimit, "%")
        };
        if (_primaryGpu >= 0)
        {
            string key = GpuSelection.Key(_snap, _primaryGpu);
            signals.Add(new(key + ":temp", PrimaryGpuName + " " + Loc.Instance["temperature"], _snap.Gpus[_primaryGpu].TemperatureC, Settings.GpuTemperatureLimit, "°C"));
            signals.Add(new(key + ":vram", PrimaryGpuName + " " + PrimaryGpuMemoryLabel, values[key + ":vram"], Settings.VramLimit, "%"));
        }
        signals.AddRange(_snap.DiskFreePercent.Select(d => new AlertSignal("disk:" + d.Key, d.Key + " " + Loc.Instance["free_space"], d.Value, Settings.DiskFreeLimit, "%", true)));
        var notices = _alerts.Evaluate(now, signals, Settings.AlertHoldSeconds, Settings.AlertCooldownSeconds);
        foreach (var notice in notices)
        {
            RecentAlerts.Insert(0, $"{notice.Time.LocalDateTime:HH:mm:ss}  {notice.Signal.Label}  {notice.Signal.Value:0.0}{notice.Signal.Unit}");
            while (RecentAlerts.Count > 20) RecentAlerts.RemoveAt(RecentAlerts.Count - 1);
        }
        if (notices.Count > 0) AlertsRaised?.Invoke(notices);
    }

    private void MarkStale()
    {
        IsStale = true;
        _lastHistory = DateTimeOffset.UtcNow;
        _history.Add(_lastHistory, new Dictionary<string, double?>());
        _alerts.Evaluate(_lastHistory, Array.Empty<AlertSignal>(), Settings.AlertHoldSeconds, Settings.AlertCooldownSeconds);
        RefreshTrend();
        OnPropertyChanged(null);
    }
}
