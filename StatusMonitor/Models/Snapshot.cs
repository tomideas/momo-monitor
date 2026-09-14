namespace StatusMonitor.Models;

/// <summary>One hardware component's instantaneous readings.</summary>
public sealed class ComponentReading
{
    public string Id { get; set; } = "";
    public bool IsIntegrated { get; set; }
    public bool UsesSharedMemory { get; set; }
    public double? LoadPercent { get; set; }
    public double? TemperatureC { get; set; }
    public double? ClockMhz { get; set; }
    public double? FanRpm { get; set; }
    public double? PowerWatts { get; set; }
    public bool PowerEstimated { get; set; }
    public double? MemoryUsedMb { get; set; }
    public double? MemoryTotalMb { get; set; }
}

/// <summary>One row of the "Top Processes" table.</summary>
public sealed class ProcessRow
{
    public string Name { get; set; } = "";
    public double CpuPercent { get; set; }
    public double? GpuPercent { get; set; }
    public double RamMb { get; set; }
    public double RamPercent { get; set; }
    public double PowerWatts { get; set; }
    public double DiskReadBytesPerSec { get; set; }
    public double DiskWriteBytesPerSec { get; set; }
}

/// <summary>A single fan / pump tachometer reading.</summary>
public sealed class FanReading
{
    public string Name { get; set; } = "";
    public double Rpm { get; set; }
}

/// <summary>One fixed volume's capacity, sampled with the 30-second disk scan.</summary>
public sealed class DiskVolume
{
    /// <summary>Root path as reported by DriveInfo, e.g. "C:\".</summary>
    public string Name { get; set; } = "";
    /// <summary>Volume label, empty when the drive has none.</summary>
    public string Label { get; set; } = "";
    public double UsedGb { get; set; }
    public double TotalGb { get; set; }
    public double FreeGb => Math.Max(0, TotalGb - UsedGb);
    public double UsedFraction => TotalGb > 0 ? Math.Clamp(UsedGb / TotalGb, 0, 1) : 0;
    public double FreePercent => TotalGb > 0 ? FreeGb * 100.0 / TotalGb : 0;
}

/// <summary>A complete sample of the whole machine.</summary>
public sealed class Snapshot
{
    /// <summary>Fixed volumes and their capacity. Refreshed every 30 seconds, not every tick.</summary>
    public List<DiskVolume> Disks { get; set; } = new();

    /// <summary>Free space per volume, derived from <see cref="Disks"/> for the alert engine.</summary>
    public Dictionary<string, double> DiskFreePercent =>
        Disks.ToDictionary(d => d.Name, d => d.FreePercent);
    public ComponentReading Cpu { get; set; } = new();
    public List<ComponentReading> Gpus { get; set; } = new();
    public List<string> GpuNames { get; set; } = new();
    public bool GpuPresent { get; set; }

    public double? RamLoadPercent { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double? RamTempC { get; set; }

    public double? StorageLoadPercent { get; set; }
    public double DiskReadBytesPerSec { get; set; }
    public double DiskWriteBytesPerSec { get; set; }
    public bool StorageIsSsd { get; set; } = true;

    public double NetDownBytesPerSec { get; set; }
    public double NetUpBytesPerSec { get; set; }

    public double CpuWatts { get; set; }
    public double GpuWatts { get; set; }
    public double RamWatts { get; set; }
    public double DiskWatts { get; set; }
    public double NetWatts { get; set; }
    public double TotalWatts { get; set; }

    /// <summary>
    /// True when any part of <see cref="TotalWatts"/> came from a curve rather than a sensor.
    /// The figure feeds the running energy total, the carbon figure and the cost, so how it was
    /// arrived at travels with it instead of being inferred at the point of display.
    /// </summary>
    public bool PowerEstimated { get; set; }

    /// <summary>
    /// True when a discrete GPU reports no power and its board power is not in the table, so
    /// its draw is in neither the measured nor the estimated part of the total. The total is
    /// then known to be low, and says so.
    /// </summary>
    public bool PowerIncomplete { get; set; }

    public double TotalEnergyWh { get; set; }
    public double CarbonGrams { get; set; }
    public double CostAmount { get; set; }
    public string CostCurrency { get; set; } = "$";

    public List<ProcessRow> Processes { get; set; } = new();
    public List<FanReading> Fans { get; set; } = new();
}
