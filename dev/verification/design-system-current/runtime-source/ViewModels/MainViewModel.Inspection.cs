using System.Windows.Input;
using StatusMonitor.I18n;
using StatusMonitor.Services;

namespace StatusMonitor.ViewModels;

public sealed partial class MainViewModel
{
    public ICommand InspectMetricCommand => new InspectCommand(this);
    public event Action<string>? InspectionRequested;
    private string _trendGpuId = "";
    public string ProcessSortKey => _service.ProcessSortKey;
    public string InspectionHint => Loc.Instance["inspect_hint"];

    private int TrendGpuIndex => string.IsNullOrEmpty(_trendGpuId) ? _primaryGpu
        : Enumerable.Range(0, _snap.Gpus.Count).FirstOrDefault(i => GpuSelection.Key(_snap, i) == _trendGpuId, -1);

    public void InspectMetric(string metric, string gpuId = "")
    {
        _trendGpuId = gpuId;
        if (metric == "disk")
        {
            SetProcessSort("disk");
            InspectionRequested?.Invoke("disk");
            return;
        }
        Settings.TrendMetric = metric;
        RefreshTrend();
        if (metric == "ram") SetProcessSort("ram");
        else if (metric is "cpu" or "cpu_temp") SetProcessSort("cpu");
        else if (metric is "gpu" or "gpu_temp" or "vram") SetProcessSort("gpu");
        OnPropertyChanged(null);
        InspectionRequested?.Invoke(metric);
    }

    public void SetProcessSort(string metric)
    {
        _service.ProcessSortKey = metric;
        Processes.Clear();
        foreach (var row in ProcessRanking.Order(_snap.Processes, metric)) Processes.Add(ToVm(row));
        ApplyInterval(dueNow: true);
        OnPropertyChanged(nameof(ProcessSortKey));
    }

    public static List<FeatureOption> ProcessSortOptions() => new()
    {
        new("power", Loc.Instance["col_power"]), new("cpu", "CPU"), new("gpu", "GPU"),
        new("ram", Loc.Instance["ram"]), new("disk", Loc.Instance["col_disk"])
    };

    public void RetryHardwareSensors()
    {
        _service.RequestHardwareRetry();
        ApplyInterval(dueNow: true);
    }

    public string SensorStatusText
    {
        get
        {
            if (IsStale) return Loc.Instance["sensor_stale"];
            if (!_service.HardwareAvailable) return Loc.Instance["sensor_failed"];
            if (_service.HardwarePartiallyAvailable) return Loc.Instance["sensor_partial"];
            if (!_service.IsElevated) return Loc.Instance["sensor_admin"];
            if (_snap.Cpu.TemperatureC is null && !SensorDriverService.IsInstalled()) return Loc.Instance["sensor_driver"];
            var missing = new List<string>();
            if (_snap.Cpu.TemperatureC is null) missing.Add("CPU " + Loc.Instance["temperature"]);
            for (int i = 0; i < _snap.Gpus.Count; i++)
                if (_snap.Gpus[i].TemperatureC is null)
                    missing.Add((i < _snap.GpuNames.Count ? _snap.GpuNames[i] : "GPU") + " " + Loc.Instance["temperature"]);
            return missing.Count > 0 ? string.Join(" / ", missing) + " · " + Loc.Instance["sensor_not_reported"] : "";
        }
    }
    public bool HasSensorStatus => SensorStatusText.Length > 0;
    public string TrendReadingHelp
    {
        get
        {
            if (IsStale) return Loc.Instance["sensor_stale"];
            if (!_service.HardwareAvailable && Settings.TrendMetric is "cpu" or "cpu_temp" or "gpu" or "gpu_temp" or "vram")
                return SensorStatusText;
            if (Settings.TrendMetric == "cpu_temp" && _snap.Cpu.TemperatureC is null) return SensorStatusText;
            if (Settings.TrendMetric is "gpu" or "gpu_temp" or "vram")
            {
                int i = TrendGpuIndex;
                if (i < 0) return Loc.Instance["gpu_unavailable"];
                var g = _snap.Gpus[i];
                bool missing = Settings.TrendMetric switch
                { "gpu_temp" => g.TemperatureC is null, "vram" => g.MemoryTotalMb is not > 0, _ => g.LoadPercent is null };
                if (missing) return Loc.Instance["sensor_not_reported"];
            }
            return Loc.Instance["trend_hint"];
        }
    }

    private sealed class InspectCommand(MainViewModel owner) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter)
        {
            if (parameter is GpuCard gpu) owner.InspectMetric("gpu_temp", gpu.Id);
            else owner.InspectMetric(parameter as string ?? "cpu_temp");
        }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
