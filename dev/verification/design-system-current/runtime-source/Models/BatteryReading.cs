namespace StatusMonitor.Models;

/// <summary>Read-only battery telemetry. Charging power is never machine consumption.</summary>
public sealed class BatteryReading
{
    public bool IsPresent { get; set; }
    public bool? IsOnAcPower { get; set; }
    public double? ChargePercent { get; set; }
    public double? ChargeWatts { get; set; }
    public double? DischargeWatts { get; set; }
    /// <summary>NoBattery, Charging, Discharging, Idle, Mixed, Unknown or Unavailable.</summary>
    public string Status { get; set; } = "Unknown";
    public string SourceName { get; set; } = "Windows battery API";
    public string? Error { get; set; }
}
