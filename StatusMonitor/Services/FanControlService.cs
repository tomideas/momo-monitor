using LibreHardwareMonitor.Hardware;
using StatusMonitor.Models;
using StatusMonitor.Sensors;
using StatusMonitor.Settings;

namespace StatusMonitor.Services;

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

    /// <summary>Minimum the hardware itself accepts. Never write below this.</summary>
    public double MinPercent { get; init; }
    public double MaxPercent { get; init; }

    /// <summary>
    /// The sensor each category resolves to on this machine, empty when absent. Chosen once at
    /// discovery so the UI offers a category and the service never has to search by name.
    /// </summary>
    public required string CpuSensorId { get; init; }
    public required string GpuSensorId { get; init; }

    /// <summary>
    /// What this fan follows: "gpu" its own card, "cpu" the package, "fan" whichever is hotter.
    /// Coarser than <see cref="Icon"/> on purpose — an AIO pump and a CPU fan are drawn
    /// differently but both serve the CPU.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Which glyph marks the row. A board reports its headers by name and nothing else,
    /// so this is read off the sensor name — the only thing the hardware actually tells us.</summary>
    public required string Icon { get; init; }

    public double? CurrentRpm { get; set; }
    public double? CurrentPercent { get; set; }
    public double? SourceTemperatureC { get; set; }
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

    public IReadOnlyList<FanTarget> Targets { get; private set; } = Array.Empty<FanTarget>();

    /// <summary>True when at least one fan on this machine can be driven by software.</summary>
    public bool Available => Targets.Count > 0;

    /// <summary>
    /// Finds every controllable fan. Called once after the hub opens; the set of fans does not
    /// change while the app runs.
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
        string cpuId = Best(cpuTemps, "Package", "Core Max", "Core Average");

        foreach (var type in types)
            foreach (var root in hub.All(type))
                foreach (var hw in HardwareMonitorHub.SelfAndSub(root))
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.Control is not { } control) continue;

                        bool onGpu = type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
                        // A GPU fan follows its own card, never a different one.
                        var ownGpu = gpuTemps.Where(g => ReferenceEquals(g.Owner, root)).Select(g => g.Sensor).ToList();
                        string gpuId = Best(ownGpu.Count > 0 ? ownGpu : gpuTemps.Select(g => g.Sensor).ToList(),
                            "Hot Spot", "Core");

                        found.Add(new FanTarget
                        {
                            Id = sensor.Identifier.ToString(),
                            HardwareName = root.Name,
                            SensorName = sensor.Name,
                            Control = control,
                            Sensor = sensor,
                            MinPercent = control.MinSoftwareValue,
                            MaxPercent = control.MaxSoftwareValue,
                            CpuSensorId = cpuId,
                            GpuSensorId = gpuId,
                            Kind = onGpu ? "gpu" : FanNaming.IsCpuServing(sensor.Name) || type == HardwareType.Cpu ? "cpu" : "fan",
                            Icon = onGpu ? "gpu" : FanNaming.IconFor(sensor.Name),
                        });
                    }

        Targets = found;
    }

    /// <summary>
    /// The first sensor whose name contains one of <paramref name="preferred"/>, else the first
    /// of the list, else empty. Preference order is the argument order.
    /// </summary>
    private static string Best(IReadOnlyList<ISensor> sensors, params string[] preferred)
    {
        foreach (string want in preferred)
        {
            var hit = sensors.FirstOrDefault(s => s.Name.Contains(want, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit.Identifier.ToString();
        }
        return sensors.Count > 0 ? sensors[0].Identifier.ToString() : "";
    }

    /// <summary>
    /// The temperature this fan follows. There is nothing here for the user to decide, so the
    /// app does not ask: a GPU fan follows its own card, a CPU fan the package, and a chassis
    /// fan whichever of the two is hotter at that moment. That last rule is the one worth
    /// stating — a case fan pinned to the CPU sits idle through a long GPU load, which on a
    /// small-volume build is exactly the case that matters, and it is why MSI's own system-fan
    /// control watches both rather than making anyone choose. A machine missing one category
    /// uses the other.
    /// </summary>
    private static double? ReadSource(HardwareMonitorHub hub, FanTarget target)
    {
        double? cpu = ReadSensor(hub, target.CpuSensorId);
        double? gpu = ReadSensor(hub, target.GpuSensorId);
        return target.Kind switch
        {
            "gpu" => gpu ?? cpu,
            "cpu" => cpu ?? gpu,
            _ => cpu is null ? gpu : gpu is null ? cpu : Math.Max(cpu.Value, gpu.Value),
        };
    }

    private static double? ReadSensor(HardwareMonitorHub hub, string wanted)
    {
        if (wanted.Length == 0) return null;
        foreach (var type in new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel,
                                     HardwareType.Motherboard, HardwareType.Cooler, HardwareType.Cpu })
            foreach (var root in hub.All(type))
                foreach (var hw in HardwareMonitorHub.SelfAndSub(root))
                    foreach (var s in hw.Sensors)
                        if (s.SensorType == SensorType.Temperature && s.Identifier.ToString() == wanted)
                            return s.Value;
        return null;
    }

    /// <summary>
    /// Refreshes what each fan is currently doing, without touching any of them. Separate from
    /// <see cref="Apply"/> so a preview run, which is forbidden to drive hardware, can still
    /// show real speeds rather than a row of dashes.
    /// </summary>
    public void ReadLive(HardwareMonitorHub hub)
    {
        lock (_lock)
        {
            foreach (var target in Targets)
            {
                target.CurrentRpm = FindFanRpm(hub, target);
                target.CurrentPercent = target.Sensor.Value;
            }
        }
    }

    /// <summary>
    /// Evaluates every enabled profile and writes the result. Call once per sampling tick,
    /// after the hub has been updated.
    /// </summary>
    public void Apply(HardwareMonitorHub hub, AppSettings settings)
    {
        lock (_lock)
        {
            foreach (var target in Targets)
            {
                var profile = settings.FanProfiles.FirstOrDefault(p => p.ControlId == target.Id);
                if (profile is null || profile.Mode == FanMode.Auto)
                {
                    Release(target);
                    target.SoftwareControlled = false;
                    target.SourceTemperatureC = null;
                    continue;
                }

                double percent;
                if (profile.Mode == FanMode.Constant)
                {
                    target.SourceTemperatureC = null;
                    percent = profile.ConstantPercent;
                }
                else
                {
                    double? temperature = ReadSource(hub, target);
                    target.SourceTemperatureC = temperature;
                    // No reading means no basis for a decision. Handing control back to the
                    // firmware is the only safe response — holding the last value would keep
                    // driving a fan from a number that may be minutes stale.
                    if (temperature is null)
                    {
                        Release(target);
                        target.SoftwareControlled = false;
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
                }
                catch
                {
                    // The driver can refuse (a resume from sleep, a driver reset). Drop the
                    // claim so the next tick re-takes it rather than assuming it still holds.
                    _taken.Remove(target.Id);
                    target.SoftwareControlled = false;
                }
            }
        }
    }

    private static double? FindFanRpm(HardwareMonitorHub hub, FanTarget target)
    {
        foreach (var type in new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel,
                                     HardwareType.Motherboard, HardwareType.Cooler, HardwareType.Cpu })
            foreach (var root in hub.All(type))
                foreach (var hw in HardwareMonitorHub.SelfAndSub(root))
                    foreach (var s in hw.Sensors)
                        if (s.SensorType == SensorType.Fan && s.Value is not null &&
                            root.Name == target.HardwareName)
                            return s.Value;
        return null;
    }

    private void Release(FanTarget target)
    {
        if (!_taken.Remove(target.Id, out var control)) return;
        try { control.SetDefault(); } catch { }
        if (_states.TryGetValue(target.Id, out var state)) state.Reset();
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
            foreach (var target in Targets) target.SoftwareControlled = false;
        }
    }
}
