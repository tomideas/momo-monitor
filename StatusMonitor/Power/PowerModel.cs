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
    // The curve differs from the CPU's at both ends, and both differences matter. A card's
    // rating is a board power limit it is held to, not a sustained figure a boost state runs
    // past, so the ceiling is the rating itself rather than 125% of it.
    //
    // The floor is not a share of the rating at all, which is the correction. A tenth came over
    // with the rest of the reference implementation's model and it only holds in the middle of
    // the range, because idle board power is far flatter than the rating is: published idle
    // figures run about 5-7 W for a 47 W card, 15-20 W for a 250 W one, and 20-25 W for a 450 W
    // one. That is a tenfold spread in rating against a fourfold spread in idle. A flat tenth
    // therefore reads low at the small end and badly high at the top, where it would have put a
    // 4090 at 45 W while doing nothing - about double the truth, and the same kind of confidently
    // wrong number this file exists to avoid.
    //
    // A fixed cost plus a small slope tracks that shape instead. The fixed part is what a card
    // spends existing - display engine, memory, fans, VRM losses - and does not scale with the
    // rating. The cap stops a large card from idling absurdly; the fraction cap keeps the floor
    // under the rating for very small boards, where the fixed cost alone would exceed it.
    private const double GpuIdleBaseW = 3.0;
    private const double GpuIdleSlope = 0.06;
    private const double GpuIdleMinW = 4.0;
    private const double GpuIdleMaxW = 25.0;
    private const double GpuIdleMaxFraction = 0.25;
    private const double GpuPeakMultiplier = 1.0;

    /// <summary>Idle board power for a card of this rating. Public so the curve can be checked at the floor.</summary>
    public static double GpuIdleWatts(double tdp)
    {
        double idle = Math.Clamp(GpuIdleBaseW + tdp * GpuIdleSlope, GpuIdleMinW, GpuIdleMaxW);
        return Math.Min(idle, tdp * GpuIdleMaxFraction);
    }

    public static double EstimateGpuWatts(double tdp, double usagePercent)
    {
        double u = Math.Clamp(usagePercent / 100.0, 0.0, 1.0);
        double idle = GpuIdleWatts(tdp);
        double peak = tdp * GpuPeakMultiplier;
        return idle + (peak - idle) * Math.Pow(u, 1.3);
    }

    // Board power for the cards this is likely to meet. Longest match wins, so "4070 Ti" is
    // tested before "4070".
    //
    // A table can never be the whole answer for software handed to strangers, and asking the
    // driver instead does not rescue it: a card that cannot report its power cannot report its
    // power limit either — both come from the same power-management block, and on the Quadro
    // that prompted this, nvidia-smi answers N/A to power.draw and power.limit alike. So the
    // three ways out are a known model, a figure the owner supplies, or an honest blank.
    //
    // What this must not do is invent a default. The reference implementation does: for a card
    // it does not recognise it assumes roughly a 130 W mid-range part, which put a 47 W Quadro
    // at a flat 15.00 W while idle — a confidently wrong number, three times the truth, and
    // indistinguishable on screen from a measured one. An unrecognised card here returns 0 and
    // is reported as not counted.
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
        ("2080 ti", 250), ("2080 super", 250), ("2080", 215),
        ("2070 super", 215), ("2070", 175), ("2060 super", 175), ("2060", 160),
        ("1660 ti", 120), ("1660 super", 125), ("1660", 120), ("1650 super", 100), ("1650", 75),
        // GeForce GTX 10 and 900
        ("1080 ti", 250), ("1080", 180), ("1070 ti", 180), ("1070", 150),
        ("1060", 120), ("1050 ti", 75), ("1050", 75), ("1030", 30),
        ("980 ti", 250), ("980", 165), ("970", 145), ("960", 120), ("950", 90),
        // Quadro and RTX A: the workstation cards most likely to report nothing at all, which
        // is how this whole path came to exist.
        ("p400", 30), ("p600", 40), ("p620", 40), ("p1000", 47),
        ("p2000", 75), ("p2200", 75), ("p4000", 105), ("p5000", 180),
        ("t400", 30), ("t600", 40), ("t1000", 50), ("t2000", 60),
        ("rtx a1000", 50), ("rtx a2000", 70), ("rtx a4000", 140),
        ("rtx a4500", 200), ("rtx a5000", 230), ("rtx a6000", 300),
        ("k1200", 45), ("k2200", 68), ("m2000", 75), ("m4000", 120),
        // Radeon RX 9000 / 7000 / 6000 / 5000
        ("9070 xt", 304), ("9070", 220),
        ("7900 xtx", 355), ("7900 xt", 315), ("7900 gre", 260),
        ("7800 xt", 263), ("7700 xt", 245), ("7600 xt", 190), ("7600", 165),
        ("6950 xt", 335), ("6900 xt", 300), ("6800 xt", 300), ("6800", 250),
        ("6750 xt", 250), ("6700 xt", 230), ("6650 xt", 180), ("6600 xt", 160),
        ("6600", 132), ("6500 xt", 107), ("6400", 53),
        ("5700 xt", 225), ("5700", 180), ("5600 xt", 150), ("5500 xt", 130),
        // Intel Arc
        ("a770", 225), ("a750", 225), ("a580", 185), ("a380", 75),
        ("b580", 190), ("b570", 150),
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

    // ---- fans ----
    // Fans were counted as nothing, and they are the one piece of board overhead with a real
    // signal behind it: the tachometers are already read for the Fans page, so this is a model
    // over a measurement rather than a guess over a guess.
    //
    // Fan power follows the affinity laws and rises with the cube of speed, which is why a case
    // fan loafing at 600 rpm costs almost nothing while the same fan at 2000 costs over a watt.
    // The anchor is the 120 mm class at full tilt: an Arctic P12 and a Noctua NF-A12 are both
    // rated 12 V x 0.14 A, so 1.7 W.
    //
    // The limit is that a tachometer does not say how big the fan is. A 40 mm chipset fan spins
    // far faster than a 140 mm case fan while drawing less, so the cube law overstates it; the
    // cap is what stops that running away. Card fans are not in this sum and must not be - only
    // motherboard tachometers are collected, and a card's own fans are already inside the board
    // power figure used for it, the same trap the integrated GPU rule exists for.
    private const double FanReferenceRpm = 1900.0;
    private const double FanReferenceW = 1.7;
    private const double FanMaxW = 4.0;

    public static double FanWatts(double rpm)
    {
        if (rpm <= 0) return 0.0;
        double ratio = rpm / FanReferenceRpm;
        return Math.Min(FanMaxW, FanReferenceW * ratio * ratio * ratio);
    }

    // ---- the board itself ----
    // Everything that draws from the supply without appearing in any figure above: the chipset,
    // the network and audio controllers, whatever is hanging off USB, and the loss in the
    // regulators feeding the CPU. Counting that as zero was never a measurement, it was an
    // omission, and after the power supply it is the largest one.
    //
    // It splits into what a board spends existing and what it spends converting. The first does
    // not move with load. The second is a share of what passes through the motherboard's
    // regulators, which run at roughly 85-92%, so about a tenth of what reaches the CPU is lost
    // as heat on the way. The card is deliberately not in that share: its rating is measured at
    // its own connectors, so its regulators are already inside the figure used for it.
    public const double BoardBaseWattsDefault = 7.0;
    private const double VrmLossFraction = 0.10;

    public static double VrmLossWatts(double cpuWatts) => Math.Max(0.0, cpuWatts) * VrmLossFraction;

    // ---- the power supply ----
    // Only meaningful once the owner says what the supply is, which is why nothing here has a
    // default that gets applied silently. A desktop idling at 30 W on a 500 W unit sits at 6%
    // load, far below the 20% where the 80 PLUS ratings start being defined, and that is the
    // part of the curve that falls off a cliff: the same unit that holds 85% mid-range can be
    // under 70% down there. Dividing by one flat efficiency is the mistake this avoids.
    //
    // Anchors are the three rated load points plus three below them, where measured units run
    // well under their badge. Between anchors it interpolates; outside them it holds.
    private static readonly double[] EfficiencyLoads = { 0.02, 0.05, 0.10, 0.20, 0.50, 1.00 };
    private static readonly (string Key, double[] Curve)[] EfficiencyCurves =
    {
        ("white",    new[] { 0.55, 0.64, 0.72, 0.80, 0.80, 0.80 }),
        ("bronze",   new[] { 0.60, 0.69, 0.77, 0.82, 0.85, 0.82 }),
        ("gold",     new[] { 0.66, 0.75, 0.83, 0.87, 0.90, 0.87 }),
        ("platinum", new[] { 0.70, 0.79, 0.86, 0.90, 0.92, 0.89 }),
        ("titanium", new[] { 0.78, 0.85, 0.90, 0.92, 0.94, 0.90 }),
    };

    public static readonly string[] EfficiencyClasses = { "white", "bronze", "gold", "platinum", "titanium" };

    /// <summary>
    /// Conversion efficiency of the supply at this draw, or 0 when the supply is unknown.
    /// Zero means "do not apply a loss" rather than "loses everything": a caller with no rating
    /// to work from must leave the figure alone instead of inventing one.
    /// </summary>
    public static double PsuEfficiency(string className, double dcWatts, double ratedWatts)
    {
        if (ratedWatts <= 0 || dcWatts <= 0) return 0.0;
        double[] curve = EfficiencyCurves.FirstOrDefault(c =>
            string.Equals(c.Key, className, StringComparison.OrdinalIgnoreCase)).Curve
            ?? EfficiencyCurves[1].Curve;

        double load = Math.Clamp(dcWatts / ratedWatts, 0.0, 1.0);
        if (load <= EfficiencyLoads[0]) return curve[0];
        for (int i = 1; i < EfficiencyLoads.Length; i++)
        {
            if (load > EfficiencyLoads[i]) continue;
            double span = EfficiencyLoads[i] - EfficiencyLoads[i - 1];
            double t = span > 0 ? (load - EfficiencyLoads[i - 1]) / span : 0.0;
            return curve[i - 1] + (curve[i] - curve[i - 1]) * t;
        }
        return curve[^1];
    }

    /// <summary>What the wall sees for this much direct current, given the supply's efficiency.</summary>
    public static double WallWatts(double dcWatts, double efficiency) =>
        efficiency > 0 ? dcWatts / efficiency : dcWatts;

    // Intel/AMD TDP lookup (WattSeal table), only used when RAPL is unavailable.
    private static readonly (string Pattern, double Tdp)[] CpuTdpTable =
    {
        // Intel desktop, by Processor Base Power. Suffix rows come before the bare model,
        // because the lookup takes the first row whose text appears in the name and every
        // suffixed part contains the bare one. Getting that order wrong is not a rounding
        // error: a K part is rated at 125 W and the same model without the K at 65, so one
        // misplaced row doubles or halves the figure that dominates the total. A "k" row also
        // covers KF and KS, which contain it.
        //
        // 11th gen (Rocket Lake) was missing entirely until now, and the fallback happened to
        // be right for the i5-11400 that exposed it while being wrong by a factor of two for
        // the i5-11600K beside it in the same range. There is no 11th gen desktop i3.
        ("i9-11900k", 125), ("i9-11900t", 35), ("i9-11900", 65),
        ("i7-11700k", 125), ("i7-11700t", 35), ("i7-11700", 65),
        ("i5-11600k", 125), ("i5-11600", 65), ("i5-11500", 65),
        ("i5-11400t", 35), ("i5-11400", 65),
        // 12th gen (Alder Lake)
        ("i9-12900k", 125), ("i9-12900t", 35), ("i9-12900", 65),
        ("i7-12700k", 125), ("i7-12700t", 35), ("i7-12700", 65),
        ("i5-12600k", 125), ("i5-12600", 65), ("i5-12500", 65),
        ("i5-12400t", 35), ("i5-12400", 65),
        ("i3-12100t", 35), ("i3-12100", 60),
        // 13th gen (Raptor Lake)
        ("i9-13900k", 125), ("i9-13900t", 35), ("i9-13900", 65),
        ("i7-13700k", 125), ("i7-13700t", 35), ("i7-13700", 65),
        ("i5-13600k", 125), ("i5-13600", 65), ("i5-13500", 65),
        ("i5-13400t", 35), ("i5-13400", 65),
        ("i3-13100t", 35), ("i3-13100", 60),
        // 14th gen (Raptor Lake Refresh)
        ("i9-14900k", 125), ("i9-14900t", 35), ("i9-14900", 65),
        ("i7-14700k", 125), ("i7-14700t", 35), ("i7-14700", 65),
        ("i5-14600k", 125), ("i5-14600", 65), ("i5-14500", 65),
        ("i5-14400t", 35), ("i5-14400", 65),
        ("i3-14100t", 35), ("i3-14100", 60),
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
