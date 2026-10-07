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
    private readonly BatterySensor _battery = new();
    private readonly EnergyIntegrator _energy = new();
    private int _energyResetRequested;
    private double _lastAwakeSeconds = AwakeClock.Seconds;
    public double ExpectedSampleSeconds { get; set; } = 1;

    private DateTime _last = DateTime.UtcNow;
    private bool _opened;
    private int _hardwareRetryRequested;
    private List<(string Id, string Name, bool Integrated, bool Shared, double? MemoryTotal)> _knownGpus = new();
    private DateTime _diskSpaceChecked = DateTime.MinValue;
    private List<Models.DiskVolume> _disks = new();

    /// <summary>Volume label lookup can throw on an unreadable drive; the capacity still counts.</summary>
    private static string SafeVolumeLabel(System.IO.DriveInfo drive)
    {
        try { return drive.VolumeLabel ?? ""; }
        catch { return ""; }
    }

    public AppSettings Settings { get; }
    public string ProcessSortKey { get; set; } = "power";
    public double TotalEnergyWh { get; private set; }
    public string CpuName { get; private set; } = "";
    public bool HardwareAvailable => _hub.Available;
    public string? HardwareError => _hub.Error;

    public bool IsElevated { get; } = CheckElevated();

    /// <summary>True when some readings (temps, fans, CPU watts) are unavailable.</summary>
    public bool Limited => !IsElevated || !_hub.Available || _hub.HasFailures;
    public bool HardwarePartiallyAvailable => _hub.Available && _hub.HasFailures;

    private static bool CheckElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public MonitorService(AppSettings settings, bool persist = true)
    {
        Settings = settings;
        AllowPersistence = persist;
        History = new EnergyHistory(persist);
        TotalEnergyWh = persist ? AppSettings.LoadTotalEnergyWh() : 0;
    }

    public Snapshot Sample()
    {
        if (Interlocked.Exchange(ref _energyResetRequested, 0) != 0) _energy.Reset();
        if (Interlocked.Exchange(ref _hardwareRetryRequested, 0) != 0)
        {
            // Runs under the normal sampling lock, off the UI thread. Release software
            // overrides before closing the driver objects that own them.
            Fans.InvalidateReadings(readOnly: !AllowFanControl);
            _hub.Dispose();
            _opened = false;
            _energy.Reset();
            _battery.Invalidate();
        }
        if (!_opened)
        {
            _hub.Open();
            _opened = true;
            string discoveredCpuName = CpuSensor.ReadName(_hub);
            if (discoveredCpuName.Length > 0) CpuName = discoveredCpuName;
        }

        bool hardwareUpdated = _hub.Update();
        if (hardwareUpdated)
        {
            // Late sensors and driver recovery keep existing rows where identities agree.
            Fans.Discover(_hub);
            Fans.ReadLive(_hub, Settings, readOnly: !AllowFanControl);
            if (AllowFanControl) Fans.Apply(_hub, Settings);
        }
        else
        {
            // Preserve fan names and editing state, but never reuse old RPM/temperature or
            // keep an override active after an incomplete hardware update.
            Fans.InvalidateReadings(readOnly: !AllowFanControl);
        }

        var now = DateTime.UtcNow;
        double awake = AwakeClock.Seconds;
        double elapsed = Math.Max(0.001, awake - _lastAwakeSeconds);
        _lastAwakeSeconds = awake;
        _last = now;

        var snap = new Snapshot { SampledAt = new DateTimeOffset(now).ToLocalTime(), Battery = _battery.Read(), Platform = _battery.PlatformName };
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
        if (hardwareUpdated && !_hub.HasFailures)
            _knownGpus = snap.Gpus.Select((gpu, index) => (gpu.Id,
                index < snap.GpuNames.Count ? snap.GpuNames[index] : "GPU", gpu.IsIntegrated,
                gpu.UsesSharedMemory, gpu.MemoryTotalMb)).ToList();
        else
        {
            // A failed update means missing readings, not that the user's cards disappeared.
            // Preserve only identity/capacity, so trend selection and diagnostics remain useful.
            for (int i = 0; i < snap.Gpus.Count; i++)
            {
                var live = snap.Gpus[i];
                _knownGpus.RemoveAll(g => g.Id == live.Id);
                _knownGpus.Add((live.Id, i < snap.GpuNames.Count ? snap.GpuNames[i] : "GPU",
                    live.IsIntegrated, live.UsesSharedMemory, live.MemoryTotalMb));
            }
            foreach (var known in _knownGpus.Where(g => snap.Gpus.All(live => live.Id != g.Id)))
            {
                snap.GpuNames.Add(known.Name);
                snap.Gpus.Add(new ComponentReading { Id = known.Id, IsIntegrated = known.Integrated,
                    UsesSharedMemory = known.Shared, MemoryTotalMb = known.MemoryTotal });
            }
            snap.GpuPresent = snap.Gpus.Count > 0;
        }
        snap.Sensors.AddRange(snap.Cpu.Sensors);
        foreach (var gpu in snap.Gpus) snap.Sensors.AddRange(gpu.Sensors);
        foreach (var board in _hub.All(HardwareType.Motherboard)) snap.Sensors.AddRange(SensorResolver.Capture(board));
        RamSensor.Read(snap);
        _storage.Read(snap);
        _network.Read(snap, elapsed);
        ReadMotherboardFans(snap);

        var (rows, sumCpu, sumGpu) = _process.Sample(elapsed);

        double cpuLoad = snap.Cpu.LoadPercent ?? sumCpu;
        double? gpuMax = snap.Gpus.Count > 0 ? snap.Gpus.Max(g => g.LoadPercent) : (double?)null;
        double gpuLoad = snap.GpuPresent ? (gpuMax ?? sumGpu) : 0.0;

        PowerAccounting.Apply(snap, CpuName, Settings);
        PowerModel.AttributeProcessPower(rows, cpuLoad, gpuLoad, snap.CpuWatts, snap.GpuWatts,
            snap.HasPowerReading ? snap.TotalWatts : 0);
        rows.RemoveAll(r => r.Name.Equals("Idle", StringComparison.OrdinalIgnoreCase));

        double totalRamMb = snap.RamTotalGb * 1024.0;
        if (totalRamMb > 0)
            foreach (var row in rows)
                row.RamPercent = row.RamMb / totalRamMb * 100.0;

        snap.Processes = ProcessRanking.Order(rows, ProcessSortKey)
            .Take(Math.Max(1, Settings.ProcessCount))
            .ToList();

        var integrated = _energy.Sample(awake, snap.SampledAt,
            snap.HasPowerReading ? snap.TotalWatts : null, snap.PowerBasis,
            Math.Max(5, ExpectedSampleSeconds * 3));
        TotalEnergyWh += integrated.EnergyWh;
        foreach (var segment in integrated.Segments)
            History.Add(segment.Day, segment.EnergyWh, segment.MonitoredSeconds, segment.MissingSeconds, segment.Basis);
        var coverage = History.Coverage(Settings.EnergyPeriod, DateOnly.FromDateTime(now.ToLocalTime()));
        snap.MonitoredSeconds = coverage.MonitoredSeconds;
        snap.MissingPowerSeconds = coverage.MissingSeconds;

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

    /// <summary>Requests a real hardware reopen on the next sampling tick.</summary>
    public void RequestHardwareRetry() => Interlocked.Exchange(ref _hardwareRetryRequested, 1);

    public void SuspendMonitoring()
    {
        _energy.Reset();
        Fans.RevertAll();
    }

    public void ResumeMonitoring()
    {
        Interlocked.Exchange(ref _energyResetRequested, 1);
        RequestHardwareRetry();
        _battery.Invalidate();
        _lastAwakeSeconds = AwakeClock.Seconds;
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
    public EnergyHistory History { get; }

    public bool AllowPersistence { get; set; } = true;

    private DateTime _lastFlush = DateTime.UtcNow;

    public void SaveTotals()
    {
        if (!AllowPersistence) return;
        AppSettings.SaveTotalEnergyWh(TotalEnergyWh);
        History.Save();
    }

    public void ResetTotals()
    {
        TotalEnergyWh = 0;
        _energy.Reset();
        History.Clear();
        SaveTotals();
    }

    /// <summary>Software fan curves. Discovered after the hub's first sensor update.</summary>
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
