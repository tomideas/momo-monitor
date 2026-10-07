using LibreHardwareMonitor.Hardware;

namespace StatusMonitor.Sensors;

/// <summary>Thin wrapper around LibreHardwareMonitor's <see cref="Computer"/>.</summary>
public sealed class HardwareMonitorHub : IDisposable
{
    private Computer? _computer;
    private readonly HashSet<IHardware> _current = new(ReferenceEqualityComparer.Instance);

    public bool Available { get; private set; }
    public string? Error { get; private set; }
    public bool HasFailures { get; private set; }

    /// <param name="everything">
    /// Opens the nodes the dashboard never reads as well. Only <c>--diag</c> wants them: it
    /// exists to dump whatever the machine exposes, so it must not be limited to what the UI
    /// happens to use today.
    /// </param>
    public void Open(bool everything = false)
    {
        Dispose();
        Error = null;
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                // Measured at 23 ms of every one-second tick for the storage node alone, and
                // nothing in the app has ever read it — disk activity comes from PhysicalDisk
                // performance counters, capacity from DriveInfo, and memory from
                // GlobalMemoryStatusEx. Enabling a sensor tree costs a probe at Open and a
                // full re-read on every Update, so unused nodes are not free.
                IsMemoryEnabled = everything,
                IsStorageEnabled = everything,
                IsControllerEnabled = everything,
            };
            _computer.Open();
            Available = true;
        }
        catch (Exception ex)
        {
            Error = ex.ToString();
            Dispose();
        }
    }

    /// <summary>
    /// Returns whether any device tree updated successfully. Failed trees are quarantined;
    /// another healthy device can still supply readings. HasFailures identifies partial updates.
    /// </summary>
    public bool Update()
    {
        if (_computer is null) return false;
        _current.Clear();
        var errors = new List<string>();
        foreach (var hw in _computer.Hardware)
        {
            try
            {
                UpdateRecursive(hw);
                foreach (var node in SelfAndSub(hw)) _current.Add(node);
            }
            catch (Exception ex) { errors.Add(ex.Message); }
        }
        HasFailures = errors.Count > 0;
        Error = HasFailures ? string.Join("; ", errors.Distinct()) : null;
        Available = _current.Count > 0;
        return Available;
    }

    private static void UpdateRecursive(IHardware hw)
    {
        hw.Update();
        foreach (var sub in hw.SubHardware) UpdateRecursive(sub);
    }

    public IHardware? Find(HardwareType type) =>
        Available ? _computer?.Hardware.FirstOrDefault(h => h.HardwareType == type && IsCurrent(h)) : null;

    public IEnumerable<IHardware> All(HardwareType type) =>
        Available ? _computer?.Hardware.Where(h => h.HardwareType == type && IsCurrent(h)) ?? Enumerable.Empty<IHardware>()
            : Enumerable.Empty<IHardware>();

    public bool IsCurrent(IHardware hardware) => _current.Contains(hardware);

    /// <summary>Identity only. Failed trees must never supply live values or curve inputs.</summary>
    public IEnumerable<IHardware> Discovered(HardwareType type) =>
        _computer?.Hardware.Where(h => h.HardwareType == type) ?? Enumerable.Empty<IHardware>();

    /// <summary>Yields a hardware and all of its sub-hardware, recursively.</summary>
    public static IEnumerable<IHardware> SelfAndSub(IHardware hw)
    {
        yield return hw;
        foreach (var sub in hw.SubHardware)
            foreach (var nested in SelfAndSub(sub))
                yield return nested;
    }

    public static float? FindValue(IHardware hw, SensorType type, string? nameContains = null)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType != type || s.Value is null || !float.IsFinite(s.Value.Value)) continue;
            if (nameContains is not null && !s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)) continue;
            return s.Value;
        }
        return null;
    }

    public static float? MaxValue(IHardware hw, SensorType type)
    {
        float? max = null;
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType != type || s.Value is null || !float.IsFinite(s.Value.Value)) continue;
            if (max is null || s.Value > max) max = s.Value;
        }
        return max;
    }

    public void Dispose()
    {
        try { _computer?.Close(); } catch { /* ignore */ }
        _computer = null;
        _current.Clear();
        HasFailures = false;
        Available = false;
    }
}
