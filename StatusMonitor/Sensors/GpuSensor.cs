using LibreHardwareMonitor.Hardware;
using StatusMonitor.Models;

namespace StatusMonitor.Sensors;

/// <summary>NVIDIA + AMD + Intel GPU telemetry via LibreHardwareMonitor.</summary>
public static class GpuSensor
{
    public static void Read(HardwareMonitorHub hub, Snapshot snap)
    {
        var pairs = new List<(ComponentReading gpu, string name)>();

        foreach (var hw in hub.All(HardwareType.GpuNvidia).Concat(hub.All(HardwareType.GpuAmd)).Concat(hub.All(HardwareType.GpuIntel)))
            pairs.Add((ReadCard(hw), hw.Name));

        pairs = pairs
            .OrderByDescending(p => p.gpu.MemoryTotalMb ?? double.NegativeInfinity)
            .ToList();

        snap.Gpus = pairs.Select(p => p.gpu).ToList();
        snap.GpuNames = pairs.Select(p => p.name).ToList();
        snap.GpuPresent = snap.Gpus.Count > 0;
    }

    private static ComponentReading ReadCard(IHardware hw)
    {
        double? gpuCoreLoad = HardwareMonitorHub.FindValue(hw, SensorType.Load, "GPU Core");
        double? d3d3dLoad = HardwareMonitorHub.FindValue(hw, SensorType.Load, "D3D 3D");

        double? power = null;
        foreach (var s in hw.Sensors)
            if (s.SensorType == SensorType.Power && s.Value is not null)
                power = (power ?? 0) + s.Value.Value;

        double? memUsed = HardwareMonitorHub.FindValue(hw, SensorType.SmallData, "GPU Memory Used");
        double? memTotal = HardwareMonitorHub.FindValue(hw, SensorType.SmallData, "GPU Memory Total");
        bool shared = false;

        if (hw.HardwareType == HardwareType.GpuIntel)
        {
            shared = memTotal is null && HardwareMonitorHub.FindValue(hw, SensorType.SmallData, "D3D Shared Memory Total") is not null;
            memUsed ??= HardwareMonitorHub.FindValue(hw, SensorType.SmallData, "D3D Shared Memory Used");
            memTotal ??= HardwareMonitorHub.FindValue(hw, SensorType.SmallData, "D3D Shared Memory Total");
        }

        double? load = Max(gpuCoreLoad, d3d3dLoad) ??
                       hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Value is not null)?.Value;

        return new ComponentReading
        {
            Id = hw.Identifier.ToString(),
            UsesSharedMemory = shared,
            IsIntegrated = Services.GpuSelection.IsKnownIntegrated(hw.Name),
            LoadPercent = load,
            TemperatureC = HardwareMonitorHub.FindValue(hw, SensorType.Temperature, "GPU Core"),
            ClockMhz = HardwareMonitorHub.FindValue(hw, SensorType.Clock, "GPU Core"),
            FanRpm = HardwareMonitorHub.FindValue(hw, SensorType.Fan, "GPU Fan 1"),
            PowerWatts = power,
            MemoryUsedMb = memUsed,
            MemoryTotalMb = memTotal,
        };
    }

    private static double? Max(double? a, double? b) =>
        a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);
}
