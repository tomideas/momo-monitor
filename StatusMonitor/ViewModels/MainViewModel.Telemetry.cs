using StatusMonitor.I18n;
using StatusMonitor.Models;
using StatusMonitor.Services;

namespace StatusMonitor.ViewModels;

public sealed record SensorDetailVm(string Name, string Value, string Range, string Id);

public sealed partial class MainViewModel
{
    public bool HasBattery => _snap.Battery.IsPresent;
    public string BatterySummary => string.Join(" · ", new[]
    {
        Loc.Instance["battery"], _snap.Battery.ChargePercent is { } percent ? $"{percent:0}%" : "—",
        Loc.Instance[_snap.Battery.IsOnAcPower switch { true => "battery_ac", false => "battery_dc", _ => "no_data" }],
        Loc.Instance["battery_" + _snap.Battery.Status.ToLowerInvariant()],
    });
    public string BatteryRateText => _snap.Battery.DischargeWatts is { } discharge
        ? $"{Loc.Instance["battery_discharge"]} {discharge:0.0} W"
        : _snap.Battery.ChargeWatts is { } charge ? $"{Loc.Instance["battery_charge"]} {charge:0.0} W · {Loc.Instance["battery_charge_note"]}"
        : Loc.Instance["battery_rate_unknown"];
    public string EnergyCoverageText
    {
        get
        {
            var coverage = _service.History.Coverage(EnergyPeriod, DateOnly.FromDateTime(DateTime.Now));
            string duration = TimeSpan.FromSeconds(coverage.MonitoredSeconds).TotalHours >= 1
                ? $"{coverage.MonitoredSeconds / 3600:0.0} h" : $"{coverage.MonitoredSeconds / 60:0.0} min";
            string text = string.Format(Loc.Instance["energy_coverage"], duration);
            if (coverage.MissingSeconds > 0) text += " · " + string.Format(Loc.Instance["energy_gaps"], coverage.MissingSeconds / 60);
            if (coverage.BasisSeconds.Count > 1) text += " · " + Loc.Instance["energy_mixed_sources"];
            if (coverage.MultipleMachines) text += " · " + Loc.Instance["energy_multiple_machines"];
            if (coverage.LegacyCoverageUnknown || _service.TotalEnergyWh - _service.History.Sum(EnergyPeriod.All, DateOnly.FromDateTime(DateTime.Now)) > 1)
                text += " · " + Loc.Instance["energy_legacy"];
            return text;
        }
    }
    private ComponentReading InspectedComponent => (Settings.TrendMetric is "gpu" or "gpu_temp" or "vram")
        && TrendGpuIndex >= 0 ? _snap.Gpus[TrendGpuIndex] : _snap.Cpu;
    public string TemperatureSourceText
    {
        get
        {
            var c = InspectedComponent;
            return Loc.Instance["sensor_temperature_source"] + " · " +
                (c.TemperatureSourceName.Length > 0 ? c.TemperatureSourceName : Loc.Instance["sensor_not_reported"]);
        }
    }
    public string PowerSourceText
    {
        get
        {
            var c = InspectedComponent;
            return Loc.Instance["sensor_power_source"] + " · " +
                (c.PowerSourceName.Length > 0 ? c.PowerSourceName + " · " + Loc.Instance["scope_" + c.PowerScope] : "—");
        }
    }
    public List<SensorDetailVm> SensorDetails => InspectedComponent.Sensors
        .Where(s => s.Type is "Temperature" or "Power")
        .Select(s => new SensorDetailVm(s.Name + (s.Type == "Power" ? " · " + Loc.Instance["scope_" + s.Scope] : ""),
            s.Value is { } v ? $"{v:0.0} {s.Unit}" : "— · " + Loc.Instance["sensor_not_reported_short"],
            $"{Loc.Instance["minimum"]} {FormatSensor(s.Minimum, s.Unit)} · {Loc.Instance["maximum"]} {FormatSensor(s.Maximum, s.Unit)}", s.Id)).ToList();
    public bool HasSensorDetails => SensorDetails.Count > 0;
    private static string FormatSensor(double? value, string unit) => value is { } v ? $"{v:0.0} {unit}" : "—";

    public void ExportSensorReport(string path) => SensorReportService.Export(path, _snap.Sensors,
        _snap.Platform, _snap.Battery, _snap.SampledAt, IsStale ? Loc.Instance["sensor_stale"] : SensorStatusText);

    internal void PreviewTelemetry(bool charging = false)
    {
        if (CanPersist) return;
        _snap.Platform = "Portable";
        _snap.Battery = new BatteryReading { IsPresent = true, IsOnAcPower = charging,
            ChargePercent = 68, ChargeWatts = charging ? 18.5 : null, DischargeWatts = charging ? null : 24.2,
            Status = charging ? "charging" : "discharging", SourceName = "Windows Battery Status" };
        _snap.Cpu.TemperatureC = 59;
        _snap.Cpu.TemperatureSourceName = "CPU Package";
        _snap.Cpu.PowerWatts = 11.2;
        _snap.Cpu.PowerScope = "cpu-package";
        _snap.Cpu.PowerSourceName = "CPU Package";
        _snap.Cpu.Sensors = new()
        {
            new() { Id = "/preview/cpu/temperature/0", Name = "CPU Package", Type = "Temperature", Unit = "°C", Value = 59, Minimum = 42, Maximum = 76, Scope = "cpu-package" },
            new() { Id = "/preview/cpu/power/0", Name = "CPU Package", Type = "Power", Unit = "W", Value = 11.2, Minimum = 4.5, Maximum = 38, Scope = "cpu-package" },
        };
        _snap.PowerBasis = charging ? "measured:cpu-gpu" : "measured:battery";
        _snap.HasPowerReading = true;
        _snap.PowerEstimated = false;
        _snap.PowerIncomplete = false;
        _snap.TotalWatts = charging ? 17.6 : 24.2;
        _snap.Sensors = _snap.Cpu.Sensors.ToList();
        OnPropertyChanged(null);
    }
}
