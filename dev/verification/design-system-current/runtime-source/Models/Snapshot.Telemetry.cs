namespace StatusMonitor.Models;

public sealed partial class Snapshot
{
    public DateTimeOffset SampledAt { get; set; } = DateTimeOffset.Now;
    public BatteryReading Battery { get; set; } = new();
    public string Platform { get; set; } = "Unknown";
    public string PowerBasis { get; set; } = "estimated:components";
    public bool HasPowerReading { get; set; }
    public List<SensorReading> Sensors { get; set; } = new();
    public double MonitoredSeconds { get; set; }
    public double MissingPowerSeconds { get; set; }
}
