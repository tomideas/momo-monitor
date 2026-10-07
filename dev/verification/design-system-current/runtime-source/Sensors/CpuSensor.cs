using LibreHardwareMonitor.Hardware;
using StatusMonitor.Models;

namespace StatusMonitor.Sensors;

public static class CpuSensor
{
    public static string ReadName(HardwareMonitorHub hub) =>
        hub.Find(HardwareType.Cpu)?.Name ?? "";

    public static ComponentReading Read(HardwareMonitorHub hub)
    {
        var reading = new ComponentReading();
        var cpu = hub.Find(HardwareType.Cpu);
        if (cpu is null) return reading;
        reading.Id = cpu.Identifier.ToString();
        reading.Sensors = SensorResolver.Capture(cpu);
        var sensors = SensorResolver.Sensors(cpu).ToList();

        reading.LoadPercent =
            HardwareMonitorHub.FindValue(cpu, SensorType.Load, "CPU Total")
            ?? HardwareMonitorHub.MaxValue(cpu, SensorType.Load);

        SensorResolver.ApplyTemperature(reading, SensorResolver.CpuTemperature(sensors));

        reading.ClockMhz = HardwareMonitorHub.MaxValue(cpu, SensorType.Clock);

        SensorResolver.ApplyPower(reading, SensorResolver.CpuPower(sensors), HardwareType.Cpu);

        reading.FanRpm = ReadCpuFan(hub);

        // LibreHardwareMonitor returns 0 (not null) for sensors it cannot read
        // without the driver; treat those as missing.
        reading.TemperatureC = Positive(reading.TemperatureC);
        reading.ClockMhz = Positive(reading.ClockMhz);
        return reading;
    }

    private static double? Positive(double? value) =>
        value is > 0 && double.IsFinite(value.Value) ? value : null;


    private static float? ReadCpuFan(HardwareMonitorHub hub)
    {
        var mb = hub.Find(HardwareType.Motherboard);
        if (mb is null) return null;

        float? fan = HardwareMonitorHub.FindValue(mb, SensorType.Fan, "CPU");
        if (fan is not null) return fan;

        // Fans usually live under the SuperIO sub-hardware.
        foreach (var sub in mb.SubHardware)
        {
            var f = HardwareMonitorHub.FindValue(sub, SensorType.Fan, "CPU");
            if (f is not null) return f;
        }

        // Fall back to the fastest spinning fan.
        float? best = HardwareMonitorHub.MaxValue(mb, SensorType.Fan);
        foreach (var sub in mb.SubHardware)
        {
            var f = HardwareMonitorHub.MaxValue(sub, SensorType.Fan);
            if (f is not null && (best is null || f > best)) best = f;
        }
        return best;
    }
}
