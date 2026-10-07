using System.Reflection;
using LibreHardwareMonitor.Hardware;
using StatusMonitor.Models;
using StatusMonitor.Sensors;

// Real LibreHardwareMonitor interfaces, but all devices/sensors are DispatchProxy mocks.
// The production Computer / native driver entry points are not compiled into this test hub.
int checks = 0;
void Check(bool value, string message)
{
    checks++;
    if (!value) throw new InvalidOperationException(message);
}

var package = Sensor("CPU Package", SensorType.Temperature, "/cpu/package", 0, 39, 88);
var coreMax = Sensor("Core Max", SensorType.Temperature, "/cpu/coremax", 62, 38, 91);
var distance = Sensor("Core Distance to TjMax", SensorType.Temperature, "/cpu/distance", 99);
var tjmax = Sensor("CPU TjMax", SensorType.Temperature, "/cpu/tjmax", 105);
var cpu = Hardware("Intel Test CPU", HardwareType.Cpu, "/cpu", package, coreMax, distance, tjmax);
var hub = new HardwareMonitorHub();
hub.Roots.Add(cpu);
var reading = CpuSensor.Read(hub);
Check(reading.TemperatureC == 62, "Zero Package hid a valid Core Max");
Check(reading.TemperatureSourceName == "Core Max" && reading.TemperatureSensorId == "/cpu/coremax",
    "CPU fallback lost its physical sensor identity");
Check(reading.TemperatureMinC == 38 && reading.TemperatureMaxC == 91, "CPU min/max came from the wrong source");
Set(package, "Value", float.NaN);
Check(CpuSensor.Read(hub).TemperatureC == 62, "NaN Package hid a valid fallback");
Set(package, "Value", float.PositiveInfinity);
Check(CpuSensor.Read(hub).TemperatureC == 62, "Infinite Package was accepted");
Set(package, "Value", 60f);
Check(CpuSensor.Read(hub).TemperatureC == 60, "Recovered Package did not resume priority");
Set(package, "Value", null);
Set(coreMax, "Value", null);
reading = CpuSensor.Read(hub);
Check(reading.TemperatureC is null && reading.TemperatureSourceName == "CPU Package",
    "A missing temperature became TjMax/distance or lost its missing-source identity");
Check(SensorResolver.CpuTemperature(new[] { distance, tjmax }) is null, "Limit-only CPU reported a real temperature");

var tctl = Sensor("Core (Tctl)", SensorType.Temperature, "/amd/tctl", 85);
var tdie = Sensor("Core (Tdie)", SensorType.Temperature, "/amd/tdie", 65);
var combined = Sensor("Core (Tctl/Tdie)", SensorType.Temperature, "/amd/combined", 76);
var ccd1 = Sensor("CCD1 (Tdie)", SensorType.Temperature, "/amd/ccd1", 82);
var ccd2 = Sensor("CCD2 (Tdie)", SensorType.Temperature, "/amd/ccd2", 90);
Check(ReferenceEquals(SensorResolver.CpuTemperature(new[] { tctl, tdie }), tdie),
    "Offset control temperature silently replaced the physical die reading");
Check(ReferenceEquals(SensorResolver.CpuTemperature(new[] { ccd2, combined, ccd1 }), combined),
    "CPU selected the hottest CCD as though it were the combined control/die sensor");
Check(ReferenceEquals(SensorResolver.CpuTemperature(new[] { ccd1, ccd2 }), ccd2),
    "CCD-only fallback did not choose the hottest valid CCD");
Set(tdie, "Value", -10f);
Check(ReferenceEquals(SensorResolver.CpuTemperature(new[] { tctl, tdie }), tctl),
    "Invalid physical die reading hid the valid named control temperature");
var amd = Hardware("AMD Ryzen Test", HardwareType.Cpu, "/amd", tctl, tdie, combined, ccd1);
var rawAmd = SensorResolver.Capture(amd);
Check(rawAmd.Single(s => s.Id == "/amd/tctl").Scope == "cpu-control" &&
    rawAmd.Single(s => s.Id == "/amd/tdie").Scope == "cpu-die" &&
    rawAmd.Single(s => s.Id == "/amd/combined").Scope == "cpu-control-die" &&
    rawAmd.Single(s => s.Id == "/amd/ccd1").Scope == "cpu-ccd", "AMD scopes were flattened into Package");

var cpuPower = Sensor("CPU Package", SensorType.Power, "/cpu/power", 0);
var corePower = Sensor("CPU Cores", SensorType.Power, "/cpu/corepower", 20);
Set(cpu, "Sensors", new[] { cpuPower, corePower });
reading = CpuSensor.Read(hub);
Check(reading.PowerWatts == 0 && reading.PowerScope == "cpu-package", "Valid zero CPU power was estimated or discarded");
Set(cpuPower, "Value", -1f);
Check(CpuSensor.Read(hub).PowerWatts is null, "Negative Package or partial Cores became total CPU power");
Set(cpu, "Sensors", new[] { corePower });
Check(CpuSensor.Read(hub).PowerWatts is null, "Core-only CPU power was mislabeled whole Package");

var gpuCore = Sensor("GPU Core", SensorType.Temperature, "/gpu/core", 42, 35, 80);
var gpuHot = Sensor("GPU Hot Spot", SensorType.Temperature, "/gpu/hotspot", 88);
var gpuMem = Sensor("GPU Memory Junction", SensorType.Temperature, "/gpu/memory", 96);
var gpu = Hardware("AMD Radeon Test", HardwareType.GpuAmd, "/gpu", gpuCore, gpuHot, gpuMem);
var gpuHub = new HardwareMonitorHub();
gpuHub.Roots.Add(gpu);
ComponentReading Card() { var snap = new Snapshot(); GpuSensor.Read(gpuHub, snap); return snap.Gpus.Single(); }
reading = Card();
Check(reading.TemperatureC == 42 && reading.TemperatureSourceName == "GPU Core", "GPU replaced Core with a hotter unrelated source");
Check(reading.HotSpotTemperatureC == 88 && reading.HotSpotSourceName == "GPU Hot Spot", "GPU Hot Spot missing from details");
Check(reading.MemoryTemperatureC == 96 && reading.MemoryTemperatureSourceName == "GPU Memory Junction", "GPU memory junction missing from details");
Check(reading.TemperatureMinC == 35 && reading.TemperatureMaxC == 80, "GPU min/max source did not match current core");
Check(ReferenceEquals(SensorResolver.GpuTemperature(new[] { gpuCore, gpuHot }, true), gpuHot),
    "Fan policy lost its explicitly named Hot Spot preference");
Set(gpuCore, "Value", 0f);
reading = Card();
Check(reading.TemperatureC == 88 && reading.TemperatureSourceName == "GPU Hot Spot", "Invalid GPU Core hid a valid hotspot fallback");
Set(gpu, "Sensors", new[] { gpuMem });
reading = Card();
Check(reading.TemperatureC is null && reading.MemoryTemperatureC == 96, "VRAM-only sensor impersonated the GPU die");

var gpuPackage = Sensor("GPU Package", SensorType.Power, "/gpu/package", 100);
var gpuCorePower = Sensor("GPU Core", SensorType.Power, "/gpu/corepower", 70);
var socPower = Sensor("GPU SoC", SensorType.Power, "/gpu/soc", 20);
var pptPower = Sensor("GPU PPT", SensorType.Power, "/gpu/ppt", 95);
Set(gpu, "Sensors", new[] { gpuPackage, gpuCorePower, socPower, pptPower });
reading = Card();
Check(reading.PowerWatts == 100 && reading.PowerSourceName == "GPU Package", "Overlapping AMD power domains were added together");
Check(reading.PowerScope == "gpu-chip" && reading.Sensors.Count(s => s.Type == "Power") == 4,
    "AMD ambiguous Package claimed board scope or dropped its raw subdomains");
var boardPower = Sensor("GPU Board Power", SensorType.Power, "/gpu/board", 150);
Set(gpu, "Sensors", new[] { gpuPackage, boardPower, gpuCorePower, socPower });
Check(Card().PowerWatts == 150 && Card().PowerScope == "gpu-board", "Explicit board total did not outrank chip Package");
Set(boardPower, "Value", 0f);
Check(Card().PowerWatts == 0, "Valid zero board power lost priority to overlapping positive chip power");
Set(boardPower, "Value", float.NaN);
Check(Card().PowerWatts == 100 && Card().PowerScope == "gpu-chip", "Invalid board sensor hid a usable named chip total");
Set(gpu, "Sensors", new[] { gpuCorePower, socPower, pptPower });
Check(Card().PowerWatts is null, "GPU chip subdomains impersonated a complete GPU total");

var connector = Sensor("12VHPWR Connector", SensorType.Power, "/nv/connector", 80);
var pin = Sensor("12VHPWR Pin 1", SensorType.Power, "/nv/pin", 30);
var nvPackage = Sensor("GPU Package", SensorType.Power, "/nv/package", 99);
var nvidia = Hardware("NVIDIA Test GPU", HardwareType.GpuNvidia, "/nv", nvPackage, connector, pin);
var selectedNv = SensorResolver.GpuPower(SensorResolver.Sensors(nvidia), HardwareType.GpuNvidia);
Check(ReferenceEquals(selectedNv, nvPackage) && SensorResolver.PowerScope(HardwareType.GpuNvidia, selectedNv!.Name) == "gpu-board",
    "NVIDIA package total was added to or replaced by connector/pin power");
var intelPackage = Sensor("GPU Package", SensorType.Power, "/intel/package", 110);
var intelTotal = Sensor("GPU Total", SensorType.Power, "/intel/total", 140);
Check(ReferenceEquals(SensorResolver.GpuPower(new[] { intelPackage, intelTotal }, HardwareType.GpuIntel), intelTotal),
    "Intel card total did not outrank overlapping chip Package");
Check(SensorResolver.PowerScope(HardwareType.GpuIntel, "GPU Package") == "gpu-chip", "Intel Package claimed whole-board scope");
var intelPp1 = Sensor("GPU Power", SensorType.Power, "/intel/pp1", 3);
Check(ReferenceEquals(SensorResolver.GpuPower(new[] { intelPp1 }, HardwareType.GpuIntel), intelPp1) &&
    SensorResolver.PowerScope(HardwareType.GpuIntel, intelPp1.Name) == "gpu-chip", "Intel iGPU PP1 lost its explicit chip reading");

var missing = Sensor("GPU Core", SensorType.Temperature, "/capture/missing", null);
var invalid = Sensor("GPU Hot Spot", SensorType.Temperature, "/capture/invalid", float.NaN);
var negativePower = Sensor("GPU Package", SensorType.Power, "/capture/negative", -5);
var zeroPower = Sensor("GPU Board Power", SensorType.Power, "/capture/zero", 0);
var negativeVoltage = Sensor("-12V", SensorType.Voltage, "/capture/voltage", -12);
var child = Hardware("Telemetry child", HardwareType.GpuAmd, "/capture/sub", invalid, negativePower, zeroPower, negativeVoltage);
var captureGpu = Hardware("Owner card", HardwareType.GpuAmd, "/capture", missing);
Set(captureGpu, "SubHardware", new[] { child });
var capture = SensorResolver.Capture(captureGpu);
Check(capture.Count == 5 && capture.All(s => s.DeviceId == "/capture" && s.DeviceName == "Owner card"),
    "Recursive capture lost the physical device identity");
Check(capture.Single(s => s.Id == "/capture/missing").State == "missing", "Absent reading was called measured zero");
Check(capture.Single(s => s.Id == "/capture/invalid").State == "invalid" && capture.Single(s => s.Id == "/capture/invalid").Value is null,
    "Non-finite sensor escaped raw capture validation");
Check(capture.Single(s => s.Id == "/capture/negative").State == "invalid", "Negative consumed watts were accepted");
Check(capture.Single(s => s.Id == "/capture/zero").Value == 0 && capture.Single(s => s.Id == "/capture/zero").State == "available",
    "Raw power report discarded valid zero");
Check(capture.Single(s => s.Id == "/capture/voltage").Value == -12 && capture.Single(s => s.Id == "/capture/voltage").Unit == "V",
    "Voltage validation incorrectly reused consumed-power positivity");
hub.Failed.Add(cpu);
Check(CpuSensor.Read(hub).TemperatureC is null && CpuSensor.Read(hub).Sensors.Count == 0,
    "Failed CPU's old sensor objects were captured as live data");
Console.WriteLine($"PASS: {checks} telemetry validity, semantic scope, source identity and raw capture checks; all hardware is mocked.");

static T New<T>(Dictionary<string, object?> values) where T : class
{
    T item = DispatchProxy.Create<T, MockProxy>();
    ((MockProxy)(object)item).Values = values;
    return item;
}
static void Set<T>(T value, string property, object? data) where T : class =>
    ((MockProxy)(object)value).Values[property] = data;
static ISensor Sensor(string name, SensorType type, string id, float? value, float? min = null, float? max = null) =>
    New<ISensor>(new() { ["Name"] = name, ["SensorType"] = type, ["Identifier"] = new Identifier(id.Trim('/').Split('/')),
        ["Value"] = value, ["Min"] = min, ["Max"] = max });
static IHardware Hardware(string name, HardwareType type, string id, params ISensor[] sensors) =>
    New<IHardware>(new() { ["Name"] = name, ["HardwareType"] = type, ["Identifier"] = new Identifier(id.Trim('/').Split('/')),
        ["Sensors"] = sensors, ["SubHardware"] = Array.Empty<IHardware>() });

public class MockProxy : DispatchProxy
{
    public Dictionary<string, object?> Values { get; set; } = new();
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) throw new InvalidOperationException("Missing mock method");
        if (targetMethod.Name.StartsWith("get_"))
        {
            if (Values.TryGetValue(targetMethod.Name[4..], out var value)) return value;
            return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
        throw new InvalidOperationException("Native/mock method must not be invoked: " + targetMethod.Name);
    }
}
