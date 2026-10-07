using LibreHardwareMonitor.Hardware;

namespace StatusMonitor.Sensors;

/// <summary>
/// Test-only discovery hub: contains interface proxies exclusively. It cannot open a computer,
/// update a real device, or issue any native hardware command.
/// </summary>
public sealed class HardwareMonitorHub
{
    public bool Available { get; set; } = true;
    public HashSet<IHardware> Failed { get; } = new(ReferenceEqualityComparer.Instance);
    public bool HasFailures => Failed.Count > 0 || !Available;
    public List<IHardware> Roots { get; } = new();
    public IEnumerable<IHardware> Discovered(HardwareType type) => Roots.Where(root => root.HardwareType == type);
    public IEnumerable<IHardware> All(HardwareType type) => Discovered(type).Where(IsCurrent);
    public IHardware? Find(HardwareType type) => All(type).FirstOrDefault();
    public bool IsCurrent(IHardware hardware) => Available && Roots.Any(root =>
        !SelfAndSub(root).Any(Failed.Contains) && SelfAndSub(root).Any(node => ReferenceEquals(node, hardware)));
    public static float? FindValue(IHardware hardware, SensorType type, string? nameContains = null) => hardware.Sensors
        .FirstOrDefault(s => s.SensorType == type && s.Value is not null &&
            (nameContains is null || s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)))?.Value;
    public static float? MaxValue(IHardware hardware, SensorType type) => hardware.Sensors
        .Where(s => s.SensorType == type && s.Value is not null).Select(s => s.Value).DefaultIfEmpty(null).Max();
    public static IEnumerable<IHardware> SelfAndSub(IHardware root)
    {
        yield return root;
        foreach (var child in root.SubHardware)
            foreach (var node in SelfAndSub(child)) yield return node;
    }
}
