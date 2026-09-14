using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using StatusMonitor.Models;
using StatusMonitor.Power;
using StatusMonitor.Sensors;
using StatusMonitor.Settings;

namespace StatusMonitor.Services;

/// <summary>Owns all sensors and produces a <see cref="Snapshot"/> on demand.</summary>
public sealed class MonitorService : IDisposable
{
        private readonly HardwareMonitorHub _hub = new();
        private readonly StorageSensor _storage = new();
    private readonly NetworkSensor _network = new();
    private readonly ProcessSensor _process = new();
    private readonly CpuClockSensor _cpuClock = new();

    private DateTime _last = DateTime.UtcNow;
    private bool _opened;
    private DateTime _diskSpaceChecked = DateTime.MinValue;
    private List<Models.DiskVolume> _disks = new();

    /// <summary>Volume label lookup can throw on an unreadable drive; the capacity still counts.</summary>
    private static string SafeVolumeLabel(System.IO.DriveInfo drive)
    {
        try { return drive.VolumeLabel ?? ""; }
        catch { return ""; }
    }

    public AppSettings Settings { get; }
    public double TotalEnergyWh { get; private set; }
    public string CpuName { get; private set; } = "";
    public bool HardwareAvailable => _hub.Available;
    public string? HardwareError => _hub.Error;

    public bool IsElevated { get; } = CheckElevated();

    /// <summary>True when some readings (temps, fans, CPU watts) are unavailable.</summary>
    public bool Limited => !IsElevated || !_hub.Available;

    private static bool CheckElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public MonitorService(AppSettings settings)
    {
        Settings = settings;
        TotalEnergyWh = AppSettings.LoadTotalEnergyWh();
    }

    public Snapshot Sample()
    {
        if (!_opened)
        {
            _hub.Open();
            _opened = true;
            CpuName = CpuSensor.ReadName(_hub);
            Fans.Discover(_hub);
        }

        _hub.Update();

        // After the update so curves act on this tick's temperatures, and guarded so a preview
        // render can never drive a fan.
        Fans.ReadLive(_hub);
        if (AllowFanControl) Fans.Apply(_hub, Settings);

        var now = DateTime.UtcNow;
        double elapsed = Math.Max(0.001, (now - _last).TotalSeconds);
        _last = now;

        var snap = new Snapshot();
        if (now - _diskSpaceChecked >= TimeSpan.FromSeconds(30))
        {
            _diskSpaceChecked = now;
            _disks = new();
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed || !drive.IsReady || drive.TotalSize <= 0) continue;
                    const double gb = 1073741824.0;
                    double totalGb = drive.TotalSize / gb;
                    _disks.Add(new Models.DiskVolume
                    {
                        Name = drive.Name,
                        Label = SafeVolumeLabel(drive),
                        TotalGb = totalGb,
                        UsedGb = totalGb - drive.AvailableFreeSpace / gb,
                    });
                }
                catch (System.IO.IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        snap.Disks = new List<Models.DiskVolume>(_disks);
        snap.Cpu = CpuSensor.Read(_hub);
        snap.Cpu.ClockMhz ??= _cpuClock.Read();
        if (string.IsNullOrEmpty(CpuName)) CpuName = CpuSensor.ReadName(_hub);

        GpuSensor.Read(_hub, snap);
        RamSensor.Read(snap);
        _storage.Read(snap);
        _network.Read(snap, elapsed);
        ReadMotherboardFans(snap);

        var (rows, sumCpu, sumGpu) = _process.Sample(elapsed);

        double cpuLoad = snap.Cpu.LoadPercent ?? sumCpu;
        double? gpuMax = snap.Gpus.Count > 0 ? snap.Gpus.Max(g => g.LoadPercent) : (double?)null;
        double gpuLoad = snap.GpuPresent ? (gpuMax ?? sumGpu) : 0.0;

        double cpuWatts = snap.Cpu.PowerWatts
                          ?? PowerModel.EstimateCpuWatts(PowerModel.LookupCpuTdp(CpuName), cpuLoad);
        snap.Cpu.PowerEstimated = snap.Cpu.PowerWatts is null;
        snap.Cpu.PowerWatts = cpuWatts;

        // A GPU that reports nothing used to contribute zero watts, which reads as a measurement
        // of nothing rather than an absence of measurement. It is now estimated the same way the
        // CPU already was, and what could not be established is carried forward as such.
        double gpuWatts = 0.0;
        for (int i = 0; i < snap.Gpus.Count; i++)
        {
            var gpu = snap.Gpus[i];
            if (gpu.PowerWatts is > 0)
            {
                gpuWatts += gpu.PowerWatts.Value;
                continue;
            }

            // An integrated GPU is on the CPU die and draws through the package, so the CPU
            // figure above already contains it. Estimating it again would count the same watts
            // twice — which is the trap on any machine with an iGPU sitting next to a card.
            if (gpu.IsIntegrated)
            {
                gpu.PowerWatts = null;
                gpu.PowerEstimated = false;
                continue;
            }

            double gpuTdp = PowerModel.LookupGpuTdp(i < snap.GpuNames.Count ? snap.GpuNames[i] : "");
            if (gpuTdp <= 0)
            {
                gpu.PowerWatts = null;
                snap.PowerIncomplete = true;
                continue;
            }

            double estimated = PowerModel.EstimateGpuWatts(gpuTdp, gpu.LoadPercent ?? 0.0);
            gpu.PowerWatts = estimated;
            gpu.PowerEstimated = true;
            gpuWatts += estimated;
        }

        snap.CpuWatts = cpuWatts;
        snap.GpuWatts = gpuWatts;
        snap.RamWatts = PowerModel.RamWatts;

        double diskMb = (snap.DiskReadBytesPerSec + snap.DiskWriteBytesPerSec) / 1048576.0;
        snap.DiskWatts = PowerModel.DiskWatts(snap.StorageIsSsd, diskMb);

        double netMb = (snap.NetDownBytesPerSec + snap.NetUpBytesPerSec) / 1048576.0;
        snap.NetWatts = PowerModel.NetworkWatts(netMb);

        snap.TotalWatts = snap.CpuWatts + snap.GpuWatts + snap.RamWatts + snap.DiskWatts + snap.NetWatts;
        // RAM is a flat constant and the disk and network models are curves, so strictly the
        // total is never wholly measured. What is worth flagging is the part big enough to
        // change the answer: an unmeasured CPU or graphics card.
        snap.PowerEstimated = snap.Cpu.PowerEstimated || snap.Gpus.Any(g => g.PowerEstimated);

        PowerModel.AttributeProcessPower(rows, cpuLoad, gpuLoad, cpuWatts, gpuWatts, snap.TotalWatts);

        rows.RemoveAll(r => r.Name.Equals("Idle", StringComparison.OrdinalIgnoreCase));

        double totalRamMb = snap.RamTotalGb * 1024.0;
        if (totalRamMb > 0)
            foreach (var row in rows)
                row.RamPercent = row.RamMb / totalRamMb * 100.0;

        snap.Processes = rows
            .OrderByDescending(r => r.PowerWatts)
            .ThenByDescending(r => r.CpuPercent)
            .Take(Math.Max(1, Settings.ProcessCount))
            .ToList();

        double deltaWh = snap.TotalWatts * elapsed / 3600.0;
        TotalEnergyWh += deltaWh;
        History.Add(DateOnly.FromDateTime(now.ToLocalTime()), deltaWh);

        // Flush periodically, not only on exit. Totals used to survive a kill by luck alone;
        // now that a day's figure can be lost rather than just a session's, that is not enough.
        if (now - _lastFlush >= TimeSpan.FromMinutes(5))
        {
            _lastFlush = now;
            SaveTotals();
        }

        // The figures shown follow the selected period; carbon and cost stay derived from the
        // current tariff, exactly as the running total always has.
        double periodWh = Settings.EnergyPeriod == EnergyPeriod.All
            ? TotalEnergyWh
            : History.Sum(Settings.EnergyPeriod, DateOnly.FromDateTime(now.ToLocalTime()));
        snap.TotalEnergyWh = periodWh;
        snap.CarbonGrams = periodWh / 1000.0 * Settings.CarbonGPerKwh;
        snap.CostAmount = periodWh / 1000.0 * Settings.PricePerKwh;
        snap.CostCurrency = Settings.CurrencySymbol;
        return snap;
    }

    /// <summary>Collects every motherboard fan / pump tachometer (SuperIO / EC).</summary>
    private void ReadMotherboardFans(Snapshot snap)
    {
        var motherboard = _hub.Find(HardwareType.Motherboard);
        if (motherboard is null) return;

        foreach (var hw in HardwareMonitorHub.SelfAndSub(motherboard))
            foreach (var sensor in hw.Sensors)
                if (sensor.SensorType == SensorType.Fan && sensor.Value is > 0)
                    snap.Fans.Add(new FanReading { Name = sensor.Name, Rpm = sensor.Value.Value });
    }

    /// <summary>Per-day watt-hours behind the week / month figures.</summary>
    public EnergyHistory History { get; } = new();

    private DateTime _lastFlush = DateTime.UtcNow;

    public void SaveTotals()
    {
        AppSettings.SaveTotalEnergyWh(TotalEnergyWh);
        History.Save();
    }

    public void ResetTotals()
    {
        TotalEnergyWh = 0;
        History.Clear();
        SaveTotals();
    }

    /// <summary>Software fan curves. Discovered once the hub is open.</summary>
    public FanControlService Fans { get; } = new();

    /// <summary>
    /// False under <c>--render</c>. A preview run must never take over a fan: it exits by
    /// shutting the dispatcher down, which is not a path that guarantees a clean hand-back.
    /// </summary>
    public bool AllowFanControl { get; set; } = true;

    public void Dispose()
    {
        // Before the hub closes, or the controls are gone and nothing can be handed back.
        Fans.RevertAll();
        _hub.Dispose();
        _storage.Dispose();
        _cpuClock.Dispose();
    }
}
