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

    // ---- GPUs without power telemetry ----
    // Plenty of cards report nothing at all: a Pascal Quadro answers nvidia-smi's power.draw
    // with N/A, and the NVML energy counter that would replace it needs Volta or newer. Those
    // cards used to contribute exactly zero watts to the total, which is not "unknown" — it is
    // a wrong number wearing the same clothes as a right one.
    //
    // The curve differs from the CPU's in both ends, and both differences matter. A GPU idles
    // far lower against its rating than a CPU does, so the floor is a tenth rather than a fifth.
    // And a card's rating is a board power limit it is held to, not a sustained figure a boost
    // state runs past, so the ceiling is the rating itself rather than 125% of it.
    private const double GpuIdleFraction = 0.10;
    private const double GpuPeakMultiplier = 1.0;

    public static double EstimateGpuWatts(double tdp, double usagePercent)
    {
        double u = Math.Clamp(usagePercent / 100.0, 0.0, 1.0);
        double idle = tdp * GpuIdleFraction;
        double peak = tdp * GpuPeakMultiplier;
        return idle + (peak - idle) * Math.Pow(u, 1.3);
    }

    // Board power for the cards this is likely to meet. Longest match wins, so "4070 Ti" is
    // tested before "4070". The list is necessarily incomplete, and that is handled honestly:
    // an unrecognised card returns 0 and is reported as unmeasured rather than given an
    // invented rating. A guessed TDP would propagate into the wattage, the running total, the
    // carbon figure and the cost, all of it looking exactly as solid as a real reading.
    private static readonly (string Pattern, double Tdp)[] GpuTdpTable =
    {
        // GeForce RTX 50
        ("5090", 575), ("5080", 360), ("5070 ti", 300), ("5070", 250),
        ("5060 ti", 180), ("5060", 145),
        // GeForce RTX 40
        ("4090", 450), ("4080", 320), ("4070 ti", 285), ("4070", 200),
        ("4060 ti", 160), ("4060", 115),
        // GeForce RTX 30
        ("3090", 350), ("3080", 320), ("3070 ti", 290), ("3070", 220),
        ("3060 ti", 200), ("3060", 170), ("3050", 130),
        // GeForce RTX 20 and GTX 16
        ("2080 ti", 250), ("2080", 215), ("2070", 175), ("2060", 160),
        ("1660", 120), ("1650", 75),
        // GeForce GTX 10
        ("1080 ti", 250), ("1080", 180), ("1070", 150), ("1060", 120),
        ("1050 ti", 75), ("1050", 75),
        // Quadro / RTX A, the small workstation cards this is most likely to meet with no
        // telemetry at all.
        ("p400", 30), ("p620", 40), ("p1000", 47), ("p2000", 75),
        ("t400", 30), ("t600", 40), ("t1000", 50),
        ("a2000", 70), ("a4000", 140),
        // Radeon RX 7000 / 6000
        ("7900 xtx", 355), ("7900 xt", 315), ("7800 xt", 263), ("7700 xt", 245),
        ("7600", 165), ("6800 xt", 300), ("6700 xt", 230), ("6600", 132),
    };

    /// <summary>Board power for a card, or 0 when the model is not in the table.</summary>
    public static double LookupGpuTdp(string gpuName)
    {
        string lower = gpuName.ToLowerInvariant();
        double best = 0;
        int bestLength = 0;
        foreach (var (pattern, tdp) in GpuTdpTable)
            if (pattern.Length > bestLength && lower.Contains(pattern))
            {
                best = tdp;
                bestLength = pattern.Length;
            }
        return best;
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
