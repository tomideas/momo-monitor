namespace LibreHardwareMonitor.Hardware;

/// <summary>
/// This source type takes precedence over the library's real Computer in the linked hub.
/// Open/Close touch counters only. The harness cannot initialize a native driver or device.
/// </summary>
public sealed class Computer
{
    public static IHardware[] NextHardware { get; set; } = Array.Empty<IHardware>();
    public static bool RejectOpen { get; set; }
    public static int Opens { get; private set; }
    public static int Closes { get; private set; }
    public bool IsCpuEnabled { get; set; }
    public bool IsGpuEnabled { get; set; }
    public bool IsMotherboardEnabled { get; set; }
    public bool IsMemoryEnabled { get; set; }
    public bool IsStorageEnabled { get; set; }
    public bool IsControllerEnabled { get; set; }
    public IHardware[] Hardware { get; private set; } = Array.Empty<IHardware>();
    public void Open()
    {
        Opens++;
        if (RejectOpen) throw new InvalidOperationException("Mock open failure");
        Hardware = NextHardware;
    }
    public void Close() { Closes++; Hardware = Array.Empty<IHardware>(); }
}
