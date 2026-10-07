namespace StatusMonitor.Models;

/// <summary>A named raw sensor. Scope describes what is measured, not a claim of accuracy.</summary>
public sealed class SensorReading
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Scope { get; set; } = "unknown";
    public double? Value { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string State { get; set; } = "missing";
}
