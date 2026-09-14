using StatusMonitor.Models;

namespace StatusMonitor.Power;

/// <summary>
/// Power estimation and per-process attribution.
/// Formulas ported from WattSeal (GPLv3): CPU TDP curve, disk/network linear models,
/// RAM constant, and proportional per-process attribution.
/// </summary>
public static class PowerModel
{
    public const double RamWatts = 5.0;
    private const double NetIdleW = 0.2;
    private const double NetWPerMb = 0.01;
    private const double NetMaxW = 3.0;

    private const double SsdIdleW = 0.05, SsdWPerMb = 0.015;
    private const double HddIdleW = 3.0, HddWPerMb = 0.035;
    private const double UnknownIdleW = 0.30, UnknownWPerMb = 0.02;

    private const double CpuIdleFraction = 0.20;   // 20% of TDP at idle
    private const double CpuPeakMultiplier = 1.25; // 125% of TDP at full load

    /// <summary>Estimates CPU package power from TDP and usage (WattSeal fallback curve).</summary>
    public static double EstimateCpuWatts(double tdp, double usagePercent)
    {
        double u = Math.Clamp(usagePercent / 100.0, 0.0, 1.0);
        double idle = tdp * CpuIdleFraction;
        double peak = tdp * CpuPeakMultiplier;
        return idle + (peak - idle) * Math.Pow(u, 1.6);
    }

    public static double DiskWatts(bool isSsd, double mbPerSec) =>
        isSsd ? SsdIdleW + mbPerSec * SsdWPerMb
              : HddIdleW + mbPerSec * HddWPerMb;

    public static double NetworkWatts(double mbPerSec) =>
        Math.Min(NetMaxW, NetIdleW + mbPerSec * NetWPerMb);

    // Intel/AMD TDP lookup (WattSeal table), only used when RAPL is unavailable.
    private static readonly (string Pattern, double Tdp)[] CpuTdpTable =
    {
        ("i9-14900", 125), ("i9-13900", 125), ("i9-12900", 125),
        ("i7-14700", 65), ("i7-13700", 65), ("i7-12700", 65),
        ("i5-14600", 65), ("i5-13600", 65), ("i5-12600", 65),
        ("i5-14400", 65), ("i5-13400", 65), ("i5-12400", 65),
        ("i3-14100", 60), ("i3-13100", 60), ("i3-12100", 60),
        // AMD Ryzen 9000 (Zen 5)
        ("9950X3D", 170), ("9950X", 170), ("9900X3D", 120), ("9900X", 120),
        ("9800X3D", 120), ("9700X", 65), ("9600X", 65),
        // AMD Ryzen 7000 (Zen 4)
        ("7950X3D", 120), ("7950X", 170), ("7900X3D", 120), ("7900X", 170),
        ("7800X3D", 120), ("7800X", 105), ("7700X", 105), ("7600X", 105),
        ("5950X", 105), ("5900X", 105), ("5800X", 105),
        ("5600X", 65), ("5600", 65),
    };

    public static double LookupCpuTdp(string cpuName)
    {
        string lower = cpuName.ToLowerInvariant();
        foreach (var (pattern, tdp) in CpuTdpTable)
            if (lower.Contains(pattern.ToLowerInvariant())) return tdp;
        return 65.0;
    }

    /// <summary>
    /// Attributes system power to processes by CPU/GPU usage share, then weights the
    /// compute share against total system power. Returns per-process watts keyed by index.
    /// </summary>
    public static void AttributeProcessPower(
        IReadOnlyList<ProcessRow> rows,
        double totalCpuUsage,
        double totalGpuUsage,
        double cpuWatts,
        double gpuWatts,
        double totalSystemWatts)
    {
        double compute = cpuWatts + gpuWatts;
        foreach (var row in rows)
        {
            double cpuEnergy = totalCpuUsage > 0 ? row.CpuPercent / totalCpuUsage * cpuWatts : 0;
            if (cpuEnergy > cpuWatts) cpuEnergy = cpuWatts;

            double gpuEnergy = 0;
            if (row.GpuPercent.HasValue && totalGpuUsage > 0)
                gpuEnergy = Math.Min(row.GpuPercent.Value / totalGpuUsage * gpuWatts, gpuWatts);

            row.PowerWatts = compute > 0
                ? Math.Min((cpuEnergy + gpuEnergy) / compute * totalSystemWatts, totalSystemWatts)
                : 0;
        }
    }
}
