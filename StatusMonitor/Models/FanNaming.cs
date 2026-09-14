namespace StatusMonitor.Models;

/// <summary>
/// What a board's fan header is, read from its name.
/// <para>
/// A motherboard reports nothing about its headers except a label — LibreHardwareMonitor hands
/// over "CPU Fan", "Chassis Fan", "AIO Pump", "Extra Flow Fan" and no other distinguishing
/// fact, so the label is all there is to classify by. Kept free of hardware types on purpose:
/// this machine exposes one GPU fan and an empty motherboard node, so the only way to check the
/// rules against a real board's names is a test.
/// </para>
/// </summary>
public static class FanNaming
{
    private static bool Has(string name, string word) =>
        name.Contains(word, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this header cools the CPU, and should therefore ramp on the package rather than
    /// on whichever of CPU and GPU happens to be hotter. A pump counts: an AIO's loop is the
    /// CPU's cooling.
    /// </summary>
    public static bool IsCpuServing(string sensorName) =>
        Has(sensorName, "CPU") || IsPump(sensorName);

    /// <summary>A pump by any of the spellings boards use: "AIO Pump", "W_PUMP+", "Water Pump".</summary>
    private static bool IsPump(string name) =>
        Has(name, "Pump") || Has(name, "AIO") || Has(name, "Water");

    /// <summary>
    /// Which glyph marks the row. Order matters: "CPU Fan" must not fall through to the plain
    /// fan, and a pump is checked first because some boards label one "CPU Pump".
    /// </summary>
    public static string IconFor(string sensorName) =>
        IsPump(sensorName) ? "pump"
        : Has(sensorName, "CPU") ? "cpufan"
        : Has(sensorName, "Flow") ? "flow"
        // "CHA" and "SYS" catch the header names boards print on the silkscreen — CHA_FAN1,
        // SYS_FAN2 — which LibreHardwareMonitor passes through unchanged on chips it has no
        // friendly name table for.
        : Has(sensorName, "Chassis") || Has(sensorName, "CHA") || Has(sensorName, "Case")
          || Has(sensorName, "System") || Has(sensorName, "SYS") ? "chassis"
        // Anything else still gets a fan, never a bare piece of hardware: every row on this page
        // is a fan, so an unrecognised label should look like one.
        : "fan";
}
