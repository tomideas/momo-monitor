using StatusMonitor.Models;
using StatusMonitor.Settings;

namespace StatusMonitor.Power;

/// <summary>Chooses non-overlapping power domains; raw device readings stay available for comparison.</summary>
public static class PowerAccounting
{
    public static void Apply(Snapshot snap, string cpuName, AppSettings settings)
    {
        bool desktop = snap.Platform == "Desktop";
        double? cpu = snap.Cpu.PowerScope == "cpu-package" ? snap.Cpu.PowerWatts : null;
        if (cpu is null && desktop)
        {
            double rating = PowerModel.LookupCpuTdp(cpuName);
            if (rating > 0)
            {
                cpu = PowerModel.EstimateCpuWatts(rating, snap.Cpu.LoadPercent ?? 0);
                if (snap.Cpu.PowerWatts is >= 0) cpu = Math.Max(cpu.Value, snap.Cpu.PowerWatts.Value);
                if (snap.Cpu.PowerWatts is null)
                {
                    snap.Cpu.PowerWatts = cpu;
                    snap.Cpu.PowerEstimated = true;
                    snap.Cpu.PowerSourceName = "CPU package model";
                    snap.Cpu.PowerScope = "cpu-package";
                }
            }
        }
        snap.PowerIncomplete = cpu is null;
        snap.CpuWatts = cpu ?? 0;
        double gpuTotal = 0;
        bool estimatedGpu = false;
        for (int i = 0; i < snap.Gpus.Count; i++)
        {
            var gpu = snap.Gpus[i];
            // Package telemetry/its model contains the integrated graphics domain. Preserve
            // its independent reading on the GPU row without adding the same energy twice.
            if (gpu.IsIntegrated) continue;
            if (gpu.PowerScope == "gpu-board" && gpu.PowerWatts is >= 0)
            {
                gpuTotal += gpu.PowerWatts.Value;
                continue;
            }
            string name = i < snap.GpuNames.Count ? snap.GpuNames[i] : "";
            double rating = settings.GpuTdpWatts.TryGetValue(name, out double supplied) && supplied > 0
                ? supplied : desktop ? PowerModel.LookupGpuTdp(name) : 0;
            if (rating > 0)
            {
                double watts = PowerModel.EstimateGpuWatts(rating, gpu.LoadPercent ?? 0);
                if (gpu.PowerWatts is >= 0) watts = Math.Max(watts, gpu.PowerWatts.Value);
                gpuTotal += watts;
                estimatedGpu = true;
                if (gpu.PowerWatts is null)
                {
                    gpu.PowerWatts = watts;
                    gpu.PowerEstimated = true;
                    gpu.PowerSourceName = "GPU board model";
                    gpu.PowerScope = "gpu-board";
                }
            }
            else
            {
                snap.PowerIncomplete = true;
                if (name.Length > 0) snap.UnratedGpus.Add(name);
            }
        }
        snap.GpuWatts = gpuTotal;
        // The desktop's peripheral curves have no validated mobile equivalent. On an AC
        // notebook/unknown chassis, only the explicitly labelled CPU+GPU scope is shown.
        snap.RamWatts = desktop ? PowerModel.RamWatts : 0;
        snap.DiskWatts = desktop ? PowerModel.DiskWatts(snap.StorageIsSsd,
            (snap.DiskReadBytesPerSec + snap.DiskWriteBytesPerSec) / 1048576) : 0;
        snap.NetWatts = desktop ? PowerModel.NetworkWatts(
            (snap.NetDownBytesPerSec + snap.NetUpBytesPerSec) / 1048576) : 0;
        snap.FanWatts = desktop ? snap.Fans.Sum(f => PowerModel.FanWatts(f.Rpm)) : 0;
        snap.BoardWatts = desktop ? Math.Max(0, settings.BoardBaseWatts) + PowerModel.VrmLossWatts(snap.CpuWatts) : 0;
        double dc = snap.CpuWatts + snap.GpuWatts + snap.RamWatts + snap.DiskWatts
            + snap.NetWatts + snap.FanWatts + snap.BoardWatts;
        snap.WallMode = desktop && settings.WallPowerMode && settings.PsuRatedWatts > 0;
        snap.TotalWatts = snap.WallMode ? PowerModel.WallWatts(dc,
            PowerModel.PsuEfficiency(settings.PsuEfficiencyClass, dc, settings.PsuRatedWatts)) : dc;
        snap.PsuLossWatts = snap.TotalWatts - dc;
        snap.PowerEstimated = desktop || snap.Cpu.PowerEstimated || estimatedGpu;
        snap.PowerBasis = snap.PowerEstimated
            ? (snap.WallMode ? "estimated:wall" : desktop ? "estimated:components" : "estimated:cpu-gpu")
            : "measured:cpu-gpu";
        snap.HasPowerReading = !snap.PowerIncomplete;
        if (snap.Battery.IsOnAcPower == false && snap.Battery.DischargeWatts is >= 0)
        {
            snap.TotalWatts = snap.Battery.DischargeWatts.Value;
            snap.HasPowerReading = true;
            snap.PowerIncomplete = false;
            snap.PowerEstimated = false;
            snap.WallMode = false;
            snap.PsuLossWatts = 0;
            snap.PowerBasis = "measured:battery";
        }
    }
}
