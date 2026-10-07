using LibreHardwareMonitor.Hardware;
using StatusMonitor.Models;

namespace StatusMonitor.Sensors;

/// <summary>One semantic selection policy for dashboard, diagnostics and fan inputs.</summary>
public static class SensorResolver
{
    public static IEnumerable<ISensor> Sensors(IHardware hardware) =>
        HardwareMonitorHub.SelfAndSub(hardware).SelectMany(h => h.Sensors);

    public static double? Temperature(ISensor? sensor) => Valid(sensor?.Value, SensorType.Temperature);
    public static double? Power(ISensor? sensor) => Valid(sensor?.Value, SensorType.Power);

    public static double? Valid(float? value, SensorType type)
    {
        if (value is not { } number || !float.IsFinite(number)) return null;
        if (type == SensorType.Temperature && number <= 0) return null;
        if (type == SensorType.Power && number < 0) return null;
        return number;
    }

    /// <summary>
    /// Valid values win before names. AMD die/control/CCD readings keep their own names;
    /// a distance to TjMax or a limit is never an actual temperature fallback.
    /// </summary>
    public static ISensor? CpuTemperature(IEnumerable<ISensor> sensors) =>
        Choose(sensors.Where(s => s.SensorType == SensorType.Temperature && !IsTemperatureLimit(s.Name)),
            s => CpuTemperatureRank(s.Name), s => Temperature(s));

    public static ISensor? GpuTemperature(IEnumerable<ISensor> sensors, bool preferHotSpot = false) =>
        Choose(sensors.Where(s => s.SensorType == SensorType.Temperature && !IsTemperatureLimit(s.Name)),
            s => GpuTemperatureRank(s.Name, preferHotSpot), s => Temperature(s));

    public static ISensor? GpuHotSpot(IEnumerable<ISensor> sensors) =>
        Choose(sensors.Where(s => s.SensorType == SensorType.Temperature && !IsTemperatureLimit(s.Name)),
            s => Contains(s.Name, "Hot Spot") || Contains(s.Name, "Hotspot") ? 0 : int.MaxValue, s => Temperature(s));

    public static ISensor? GpuMemoryTemperature(IEnumerable<ISensor> sensors) =>
        Choose(sensors.Where(s => s.SensorType == SensorType.Temperature && !IsTemperatureLimit(s.Name)),
            s => Contains(s.Name, "Memory Junction") ? 0 :
                Contains(s.Name, "GPU Memory") || Contains(s.Name, "VRAM") ? 1 : int.MaxValue, s => Temperature(s));

    public static ISensor? CpuPower(IEnumerable<ISensor> sensors) =>
        Choose(sensors.Where(s => s.SensorType == SensorType.Power),
            s => Contains(s.Name, "Package") ? 0 : int.MaxValue, s => Power(s));

    /// <summary>Prefer a board total, then a named GPU package. Never sum overlapping domains.</summary>
    public static ISensor? GpuPower(IEnumerable<ISensor> sensors, HardwareType hardwareType) =>
        Choose(sensors.Where(s => s.SensorType == SensorType.Power),
            s => PowerScope(hardwareType, s.Name) == "gpu-board" ? 0 :
                IsGpuPackage(s.Name) || hardwareType == HardwareType.GpuIntel &&
                    s.Name.Equals("GPU Power", StringComparison.OrdinalIgnoreCase) ? 1 : int.MaxValue, s => Power(s));

    public static string PowerScope(HardwareType type, string name)
    {
        if (type == HardwareType.Cpu)
            return Contains(name, "Package") ? "cpu-package" : "cpu-domain";
        if (type is not (HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)) return "unknown";
        if (Contains(name, "Board") || name.Equals("GPU Total", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Total GPU Power", StringComparison.OrdinalIgnoreCase)) return "gpu-board";
        // LHM NVIDIA GPU Package is the vendor's complete-card power reading. AMD's same
        // label can be ASIC or board power depending on the API/card: retain chip scope.
        if (IsGpuPackage(name)) return type == HardwareType.GpuNvidia ? "gpu-board" : "gpu-chip";
        if (Contains(name, "Core") || Contains(name, "SoC") || Contains(name, "PPT") ||
            name.Equals("GPU Power", StringComparison.OrdinalIgnoreCase)) return "gpu-chip";
        return "unknown";
    }

    public static void ApplyTemperature(ComponentReading reading, ISensor? sensor)
    {
        reading.TemperatureC = Temperature(sensor);
        reading.TemperatureSourceName = sensor?.Name ?? "";
        reading.TemperatureSensorId = sensor?.Identifier.ToString() ?? "";
        reading.TemperatureMinC = Valid(sensor?.Min, SensorType.Temperature);
        reading.TemperatureMaxC = Valid(sensor?.Max, SensorType.Temperature);
    }

    public static void ApplyPower(ComponentReading reading, ISensor? sensor, HardwareType type)
    {
        reading.PowerWatts = Power(sensor);
        reading.PowerSourceName = sensor?.Name ?? "";
        reading.PowerSensorId = sensor?.Identifier.ToString() ?? "";
        reading.PowerScope = sensor is null ? "unknown" : PowerScope(type, sensor.Name);
    }

    public static List<SensorReading> Capture(IHardware hardware) => Sensors(hardware).Select(sensor =>
    {
        double? value = Valid(sensor.Value, sensor.SensorType);
        return new SensorReading
        {
            DeviceId = hardware.Identifier.ToString(), DeviceName = hardware.Name,
            Id = sensor.Identifier.ToString(), Name = sensor.Name, Type = sensor.SensorType.ToString(),
            Unit = Unit(sensor.SensorType), Scope = Scope(hardware.HardwareType, sensor), Value = value,
            Minimum = Valid(sensor.Min, sensor.SensorType), Maximum = Valid(sensor.Max, sensor.SensorType),
            State = sensor.Value is null ? "missing" : value is null ? "invalid" : "available",
        };
    }).ToList();

    private static ISensor? Choose(IEnumerable<ISensor> sensors, Func<ISensor, int> rank, Func<ISensor, double?> value) =>
        sensors.Select(s => (Sensor: s, Rank: rank(s), Value: value(s)))
            .Where(s => s.Rank != int.MaxValue)
            .OrderBy(s => s.Value is null).ThenBy(s => s.Rank).ThenByDescending(s => s.Value)
            .ThenBy(s => s.Sensor.Identifier.ToString(), StringComparer.Ordinal)
            .Select(s => s.Sensor).FirstOrDefault();

    private static bool IsTemperatureLimit(string name) => Contains(name, "Distance") || Contains(name, "TjMax") ||
        Contains(name, "Limit") || Contains(name, "Threshold") || Contains(name, "Critical") || Contains(name, "Delta");

    private static int CpuTemperatureRank(string name)
    {
        if (Contains(name, "Package")) return 0;
        if (Contains(name, "Tctl/Tdie")) return 1;
        if (Contains(name, "Tdie") && !Contains(name, "CCD")) return 2;
        if (Contains(name, "Tctl")) return 3;
        if (Contains(name, "Core Max") || Contains(name, "Cores Max")) return 4;
        if (Contains(name, "CCDs Max")) return 5;
        if (Contains(name, "CCD") && !Contains(name, "Average")) return 6;
        if (Contains(name, "Core Average") || Contains(name, "Cores Average")) return 7;
        if (Contains(name, "Core")) return 8;
        return 9;
    }

    private static int GpuTemperatureRank(string name, bool preferHotSpot)
    {
        if (Contains(name, "Hot Spot") || Contains(name, "Hotspot")) return preferHotSpot ? 0 : 1;
        if (name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) || name.Equals("GPU", StringComparison.OrdinalIgnoreCase))
            return preferHotSpot ? 1 : 0;
        return int.MaxValue;
    }

    private static string Scope(HardwareType type, ISensor sensor)
    {
        if (sensor.SensorType == SensorType.Power) return PowerScope(type, sensor.Name);
        if (sensor.SensorType != SensorType.Temperature) return "unknown";
        if (IsTemperatureLimit(sensor.Name)) return "temperature-limit";
        if (type == HardwareType.Cpu)
        {
            if (Contains(sensor.Name, "CCD")) return "cpu-ccd";
            if (Contains(sensor.Name, "Tctl/Tdie")) return "cpu-control-die";
            if (Contains(sensor.Name, "Tdie")) return "cpu-die";
            if (Contains(sensor.Name, "Tctl")) return "cpu-control";
            if (Contains(sensor.Name, "Package")) return "cpu-package";
            return "cpu-core";
        }
        if (type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
        {
            if (Contains(sensor.Name, "Hot Spot") || Contains(sensor.Name, "Hotspot")) return "gpu-hotspot";
            if (Contains(sensor.Name, "Memory") || Contains(sensor.Name, "VRAM")) return "gpu-memory";
            if (GpuTemperatureRank(sensor.Name, false) == 0) return "gpu-core";
        }
        return "unknown";
    }

    private static string Unit(SensorType type) => type switch
    {
        SensorType.Temperature => "°C", SensorType.Power => "W", SensorType.Load or SensorType.Control => "%",
        SensorType.Clock => "MHz", SensorType.Fan => "RPM", SensorType.Voltage => "V", SensorType.Current => "A",
        SensorType.SmallData => "MB", SensorType.Data => "GB", _ => "",
    };

    private static bool IsGpuPackage(string name) => name.Equals("GPU Package", StringComparison.OrdinalIgnoreCase);
    private static bool Contains(string value, string text) => value.Contains(text, StringComparison.OrdinalIgnoreCase);
}
