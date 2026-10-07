using LibreHardwareMonitor.Hardware;
using StatusMonitor.Models;
using StatusMonitor.Sensors;
using StatusMonitor.Settings;

namespace StatusMonitor.Services;

/// <summary>The result of the last attempt to control a fan, independent of its saved mode.</summary>
public enum FanControlStatus
{
    Firmware,
    Software,
    WaitingForApply,
    SourceUnavailable,
    DriverRejected,
    FirmwareReleaseRejected,
    ReadOnly,
}

public enum FanReadingStatus { Available, Stopped, MissingSensor, NoReading }

/// <summary>A fan the machine will let software drive, plus the temperatures it can follow.</summary>
public sealed class FanTarget
{
    public required string Id { get; init; }
    public required string HardwareName { get; init; }
    public required string SensorName { get; init; }
    public required IControl Control { get; init; }

    /// <summary>
    /// The control sensor itself. <see cref="IControl"/> exposes only what software asked for;
    /// the value the fan is actually running at reads off the sensor.
    /// </summary>
    public required ISensor Sensor { get; init; }

    /// <summary>
    /// The hardware node that owns both the PWM control and its tachometer. Some drivers add
    /// the tachometer after the control, so keeping the node lets a later sample pair it again.
    /// </summary>
    public required IHardware Hardware { get; init; }

    /// <summary>Minimum the hardware itself accepts. Never write below this.</summary>
    public double MinPercent { get; init; }
    public double MaxPercent { get; init; }

    /// <summary>
    /// The sensor each category resolves to on this machine, empty when absent. Refreshed at
    /// discovery so delayed temperature sensors and driver recovery update an existing row.
    /// </summary>
    public required string CpuSensorId { get; set; }
    public required string GpuSensorId { get; set; }

    /// <summary>
    /// Automatic source: "gpu" follows its own card; "cpu" and board "fan" follow the CPU.
    /// Coarser than <see cref="Icon"/> on purpose — an AIO pump and a CPU fan are drawn
    /// differently but both serve the CPU.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Which glyph marks the row. A board reports its headers by name and nothing else,
    /// so this is read off the sensor name — the only thing the hardware actually tells us.</summary>
    public required string Icon { get; init; }

    public ISensor? RpmSensor { get; set; }
    public double? CurrentRpm { get; set; }
    public double? CurrentPercent { get; set; }
    public double? SourceTemperatureC { get; set; }
    public string SourceSensorName { get; set; } = "";
    public string SourceCategory { get; set; } = "";
    public FanReadingStatus RpmStatus { get; set; } = FanReadingStatus.MissingSensor;
    public FanReadingStatus TemperatureStatus { get; set; } = FanReadingStatus.MissingSensor;
    public FanControlStatus ControlStatus { get; set; } = FanControlStatus.WaitingForApply;
    public bool SoftwareControlled { get; set; }
}

/// <summary>
/// Drives fan speed from a temperature curve.
/// <para>
/// This is the one place the app writes to hardware rather than reading it, so the rules are
/// strict. Nothing is driven unless the user enabled that specific fan. Every value is clamped
/// to the range the hardware advertises, so a curve can never ask for a speed the firmware
/// would refuse. Anything this class takes over is handed back on the way out — see
/// <see cref="RevertAll"/>, which is called from the normal shutdown path, from process exit
/// and from the unhandled-exception handler, because a fan left pinned low by a crashed
/// process is the failure that actually damages hardware.
/// </para>
/// <para>
/// The mechanism is the same one MSI Afterburner uses: LibreHardwareMonitor's
/// <see cref="IControl.SetSoftware"/> calls into NVAPI (NVIDIA) or ADL (AMD) to override the
/// card's own firmware fan table, and the value is rewritten on every sampling tick.
/// </para>
/// </summary>
public sealed class FanControlService
{
    private readonly Dictionary<string, FanCurveState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IControl> _taken = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private int _revision;

    public IReadOnlyList<FanTarget> Targets { get; private set; } = Array.Empty<FanTarget>();

    /// <summary>Changes only when controllable fan rows are added, removed, or replaced.</summary>
    public int Revision => Volatile.Read(ref _revision);

    /// <summary>True when at least one fan on this machine can be driven by software.</summary>
    public bool Available => Targets.Count > 0;

    /// <summary>
    /// Finds every controllable fan. Safe to call after every hardware update: stable targets
    /// keep their object identity, while delayed sensors, USB controllers and driver resets are
    /// picked up without restarting the app.
    /// </summary>
    public void Discover(HardwareMonitorHub hub)
    {
        var found = new List<FanTarget>();
        var types = new[]
        {
            HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel,
            HardwareType.Motherboard, HardwareType.Cooler, HardwareType.Cpu,
        };

        // Every temperature on the machine, grouped into the two categories a fan can follow.
        // A case fan hangs off the motherboard and has no temperature of its own, but "follow
        // the CPU" is exactly what it should do, so the categories are machine-wide.
        var cpuTemps = new List<ISensor>();
        var gpuTemps = new List<(ISensor Sensor, IHardware Owner)>();
        foreach (var type in types)
            foreach (var root in hub.All(type))
                foreach (var hw in HardwareMonitorHub.SelfAndSub(root))
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.SensorType != SensorType.Temperature) continue;
                        if (type == HardwareType.Cpu) cpuTemps.Add(sensor);
                        else if (type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                            gpuTemps.Add((sensor, root));
                    }

        // One sensor per category, picked here so the rest of the app never searches by name.
        // Package temperature for the CPU and hot spot for a GPU are the readings a fan should
        // react to: the per-core values spike on any single busy thread, and a GPU's hot spot
        // leads its core temperature, which is the whole point of reacting early.
        string cpuId = SensorResolver.CpuTemperature(cpuTemps)?.Identifier.ToString() ?? "";

        foreach (var type in types)
            foreach (var root in hub.All(type))
                foreach (var hw in HardwareMonitorHub.SelfAndSub(root))
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.Control is not { } control) continue;

                        bool onGpu = type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
                        // A GPU fan follows its own card, never a different one.
                        var ownGpu = gpuTemps.Where(g => ReferenceEquals(g.Owner, root)).Select(g => g.Sensor).ToList();
                        string gpuId = SensorResolver.GpuTemperature(onGpu ? ownGpu : gpuTemps.Select(g => g.Sensor),
                            preferHotSpot: true)?.Identifier.ToString() ?? "";

                        found.Add(new FanTarget
                        {
                            Id = sensor.Identifier.ToString(),
                            HardwareName = root.Name,
                            SensorName = sensor.Name,
                            Control = control,
                            Sensor = sensor,
                            Hardware = hw,
                            RpmSensor = MatchRpmSensor(hw, sensor),
                            MinPercent = control.MinSoftwareValue,
                            MaxPercent = control.MaxSoftwareValue,
                            CpuSensorId = cpuId,
                            GpuSensorId = gpuId,
                            Kind = onGpu ? "gpu" : FanNaming.IsCpuServing(sensor.Name) || type == HardwareType.Cpu ? "cpu" : "fan",
                            Icon = onGpu ? "gpu" : FanNaming.IconFor(sensor.Name),
                        });
                    }

        lock (_lock)
        {
            // A failed tree is still installed hardware. Retain its rows and editing identity,
            // but never inspect its old sensor objects to build a new live candidate.
            var discoveredNodes = types.SelectMany(hub.Discovered).SelectMany(HardwareMonitorHub.SelfAndSub)
                .ToHashSet(ReferenceEqualityComparer.Instance);
            var foundIds = found.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            found.AddRange(Targets.Where(t => !hub.IsCurrent(t.Hardware) && discoveredNodes.Contains(t.Hardware)
                && !foundIds.Contains(t.Id)));
            var current = Targets.ToDictionary(t => t.Id, StringComparer.Ordinal);
            var merged = new List<FanTarget>(found.Count);
            bool changed = Targets.Count != found.Count;

            foreach (var candidate in found)
            {
                if (current.TryGetValue(candidate.Id, out var existing) &&
                    ReferenceEquals(existing.Sensor, candidate.Sensor) &&
                    ReferenceEquals(existing.Hardware, candidate.Hardware) &&
                    ReferenceEquals(existing.Control, candidate.Control))
                {
                    // A late tachometer can appear without changing the PWM control. Refresh
                    // the pairing in place so an open Fans page keeps its edit state.
                    if (candidate.RpmSensor is not null)
                        existing.RpmSensor = candidate.RpmSensor;
                    existing.CpuSensorId = candidate.CpuSensorId;
                    existing.GpuSensorId = candidate.GpuSensorId;
                    merged.Add(existing);
                    continue;
                }

                if (_taken.TryGetValue(candidate.Id, out var pending) && !ReferenceEquals(pending, candidate.Control))
                {
                    // Reopening a failed driver replaces its control handles. Closing the old
                    // computer is not proof that its override was released. Keep the pending
                    // obligation on the same physical channel and retry SetDefault through its
                    // new handle; only an accepted call may report firmware control again.
                    _taken[candidate.Id] = candidate.Control;
                    candidate.SoftwareControlled = true;
                    candidate.ControlStatus = FanControlStatus.FirmwareReleaseRejected;
                }
                merged.Add(candidate);
                changed = true;
            }

            if (!changed)
                changed = !Targets.Select(t => t.Id).SequenceEqual(merged.Select(t => t.Id));

            if (!changed) return;

            var remaining = found.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var removed in Targets.Where(t => !remaining.Contains(t.Id)))
                Release(removed);

            Targets = merged.ToArray();
            Interlocked.Increment(ref _revision);
        }
    }

    /// <summary>
    /// Reads the selected monitoring source regardless of the saved control mode. The
    /// automatic source uses this fan's card or the CPU package for a board fan.
    /// A missing selected source stays unavailable instead of substituting another category.
    /// </summary>
    private static void ReadSource(HardwareMonitorHub hub, FanTarget target)
    {
        var cpu = FindSensor(hub, target.CpuSensorId);
        var gpu = FindSensor(hub, target.GpuSensorId);
        string category = FanFollow.Category(target.Kind);
        ISensor? selected = category == "gpu" ? gpu : cpu;

        target.SourceTemperatureC = TemperatureValue(selected);
        target.SourceSensorName = selected?.Name ?? "";
        target.SourceCategory = category;
        target.TemperatureStatus = selected is null ? FanReadingStatus.MissingSensor
            : target.SourceTemperatureC is null ? FanReadingStatus.NoReading : FanReadingStatus.Available;
    }

    private static double? TemperatureValue(ISensor? sensor) =>
        SensorResolver.Temperature(sensor);

    private static double? LiveValue(ISensor? sensor) =>
        sensor?.Value is { } value && float.IsFinite(value) && value >= 0 ? value : null;

    private static ISensor? FindSensor(HardwareMonitorHub hub, string wanted)
    {
        if (wanted.Length == 0) return null;
        foreach (var type in new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel,
                                     HardwareType.Motherboard, HardwareType.Cooler, HardwareType.Cpu })
            foreach (var root in hub.All(type))
                foreach (var hw in HardwareMonitorHub.SelfAndSub(root))
                    foreach (var s in hw.Sensors)
                        if (s.SensorType == SensorType.Temperature && s.Identifier.ToString() == wanted)
                            return s;
        return null;
    }

    /// <summary>
    /// Refreshes what each fan is currently doing, without touching any of them. Separate from
    /// <see cref="Apply"/> so a preview run, which is forbidden to drive hardware, can still
    /// show real speeds rather than a row of dashes.
    /// </summary>
    public void ReadLive(HardwareMonitorHub hub, AppSettings? settings = null, bool readOnly = false)
    {
        if (!hub.Available) { InvalidateReadings(readOnly); return; }
        lock (_lock)
        {
            foreach (var target in Targets)
            {
                if (!hub.IsCurrent(target.Hardware))
                {
                    Invalidate(target, readOnly);
                    continue;
                }
                // SuperIO, USB and vendor GPU drivers do not all publish controls and RPM
                // sensors in the same update. Retry an unresolved or invalidated pairing;
                // a stopped fan normally reports 0, so it does not cause needless rematching.
                if (target.RpmSensor?.Value is null)
                    target.RpmSensor = MatchRpmSensor(target.Hardware, target.Sensor);
                target.CurrentRpm = LiveValue(target.RpmSensor);
                target.CurrentPercent = LiveValue(target.Sensor);
                target.RpmStatus = target.RpmSensor is null ? FanReadingStatus.MissingSensor
                    : target.CurrentRpm is null ? FanReadingStatus.NoReading
                    : target.CurrentRpm == 0 ? FanReadingStatus.Stopped : FanReadingStatus.Available;
                ReadSource(hub, target);
                if (readOnly)
                {
                    target.SoftwareControlled = false;
                    target.ControlStatus = FanControlStatus.ReadOnly;
                }
            }
        }
    }

    /// <summary>
    /// Evaluates every enabled profile and writes the result. Call once per sampling tick,
    /// after the hub has been updated.
    /// </summary>
    public void Apply(HardwareMonitorHub hub, AppSettings settings)
    {
        if (!hub.Available) { InvalidateReadings(); return; }
        lock (_lock)
        {
            foreach (var target in Targets)
            {
                if (!hub.IsCurrent(target.Hardware))
                {
                    Invalidate(target, readOnly: false);
                    continue;
                }
                var profile = settings.FanProfiles.FirstOrDefault(p => p.ControlId == target.Id);
                ReadSource(hub, target);
                if (profile is null || profile.Mode == FanMode.Auto)
                {
                    bool released = Release(target);
                    target.SoftwareControlled = !released;
                    target.ControlStatus = released ? FanControlStatus.Firmware : FanControlStatus.FirmwareReleaseRejected;
                    continue;
                }

                double percent;
                if (profile.Mode == FanMode.Constant)
                {
                    percent = profile.ConstantPercent;
                }
                else
                {
                    double? temperature = target.SourceTemperatureC;
                    // No reading means no basis for a decision. Handing control back to the
                    // firmware is the only safe response — holding the last value would keep
                    // driving a fan from a number that may be minutes stale.
                    if (temperature is null)
                    {
                        bool released = Release(target);
                        target.SoftwareControlled = !released;
                        target.ControlStatus = released ? FanControlStatus.SourceUnavailable
                            : FanControlStatus.FirmwareReleaseRejected;
                        continue;
                    }

                    if (!_states.TryGetValue(target.Id, out var state))
                        _states[target.Id] = state = new FanCurveState();

                    var (startC, maxC) = profile.Window;
                    var ramp = FanCurve.Ramp(startC, maxC, target.MinPercent, target.MaxPercent);
                    // The top of the ramp is already full speed, so it is its own failsafe and
                    // no separate threshold is needed; passing it keeps the hysteresis from
                    // holding the fan low on the way up past it.
                    percent = state.Next(ramp, temperature.Value, profile.HysteresisC, maxC);
                }

                percent = Math.Clamp(percent, target.MinPercent, target.MaxPercent);

                try
                {
                    target.Control.SetSoftware((float)percent);
                    _taken[target.Id] = target.Control;
                    target.SoftwareControlled = true;
                    target.ControlStatus = FanControlStatus.Software;
                }
                catch
                {
                    // A write can be refused after a driver reset or resume. Attempt firmware
                    // fallback and retain ownership on a failed release so later ticks retry it.
                    _taken[target.Id] = target.Control;
                    bool released = Release(target);
                    target.SoftwareControlled = !released;
                    target.ControlStatus = released ? FanControlStatus.DriverRejected
                        : FanControlStatus.FirmwareReleaseRejected;
                }
            }
        }
    }

    /// <summary>
    /// Retains identifiable rows while clearing readings after a failed hardware update.
    /// Curves and fixed speeds are relinquished rather than driven from stale sensor values.
    /// </summary>
    public void InvalidateReadings(bool readOnly = false)
    {
        lock (_lock)
        {
            foreach (var target in Targets)
                Invalidate(target, readOnly);
        }
    }

    private void Invalidate(FanTarget target, bool readOnly)
    {
        target.CurrentRpm = null;
        target.CurrentPercent = null;
        target.SourceTemperatureC = null;
        target.RpmStatus = target.RpmSensor is null ? FanReadingStatus.MissingSensor : FanReadingStatus.NoReading;
        target.TemperatureStatus = target.SourceSensorName.Length == 0
            ? FanReadingStatus.MissingSensor : FanReadingStatus.NoReading;
        bool released = readOnly || Release(target);
        target.SoftwareControlled = !released;
        target.ControlStatus = readOnly ? FanControlStatus.ReadOnly
            : released ? FanControlStatus.SourceUnavailable : FanControlStatus.FirmwareReleaseRejected;
    }

    // Match within the owning hardware, never by the motherboard's display name. ISensor.Index
    // is not a fan channel: NVAPI's P1000 fallback reports Fan|GPU at index 1 and Control|GPU Fan
    // at index 0, even though they are the same physical fan.
    private static ISensor? MatchRpmSensor(IHardware hardware, ISensor control)
    {
        var fans = hardware.Sensors.Where(s => s.SensorType == SensorType.Fan).ToList();
        var named = fans.Where(s => string.Equals(s.Name, control.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 1) return named[0];

        string key = FanNaming.RpmMatchKey(control.Name);
        var semantic = key.Length == 0 ? new List<ISensor>()
            : fans.Where(s => FanNaming.RpmMatchKey(s.Name) == key).ToList();
        if (semantic.Count == 1) return semantic[0];

        // A single tachometer under this hardware cannot belong to another control.
        if (fans.Count == 1) return fans[0];

        // Last resort for unnamed multi-fan devices: LHM creates both arrays in cooler order.
        // Only use it when cardinality agrees, so an unrelated controllable sensor cannot shift it.
        var controls = hardware.Sensors.Where(s => s.Control is not null).ToList();
        int ordinal = controls.FindIndex(s => ReferenceEquals(s, control));
        return controls.Count == fans.Count && ordinal >= 0 ? fans[ordinal] : null;
    }

    private bool Release(FanTarget target)
    {
        if (!_taken.TryGetValue(target.Id, out var control)) return true;
        try { control.SetDefault(); }
        catch { return false; }
        _taken.Remove(target.Id);
        if (_states.TryGetValue(target.Id, out var state)) state.Reset();
        return true;
    }

    /// <summary>
    /// Hands every fan back to its firmware. Safe to call repeatedly and from any thread; it
    /// must never throw, because it runs on shutdown paths that have nowhere to report to.
    /// </summary>
    public void RevertAll()
    {
        lock (_lock)
        {
            foreach (var control in _taken.Values)
            {
                try { control.SetDefault(); } catch { }
            }
            _taken.Clear();
            foreach (var state in _states.Values) state.Reset();
            foreach (var target in Targets)
            {
                target.SoftwareControlled = false;
                target.ControlStatus = FanControlStatus.Firmware;
            }
        }
    }
}
