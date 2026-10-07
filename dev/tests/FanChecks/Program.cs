using System.Reflection;
using LibreHardwareMonitor.Hardware;
using StatusMonitor.I18n;
using StatusMonitor.Sensors;
using StatusMonitor.Services;
using StatusMonitor.Settings;
using StatusMonitor.ViewModels;

// Every IHardware / ISensor / IControl below is a DispatchProxy; the production write path
// is exercised against counters and deliberate exceptions only. No real hub is linked in.
Loc.Instance.SetLanguage("en");
int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

var package = Sensor("CPU Package", SensorType.Temperature, "/mock/cpu/package", 59);
var cpu = Hardware("Test CPU", HardwareType.Cpu, package);
var control = New<IControl>(new() { ["MinSoftwareValue"] = 20f, ["MaxSoftwareValue"] = 100f });
int softwareWrites = 0, defaultWrites = 0;
bool rejectSoftware = false, rejectDefault = false;
Proxy(control).Calls["SetSoftware"] = _ =>
{
    softwareWrites++;
    if (rejectSoftware) throw new InvalidOperationException("Mock driver rejection");
    return null;
};
Proxy(control).Calls["SetDefault"] = _ =>
{
    defaultWrites++;
    if (rejectDefault) throw new InvalidOperationException("Mock release rejection");
    return null;
};
var pwm = Sensor("CPU Fan", SensorType.Control, "/mock/board/control", 93, control);
var rpm = Sensor("CPU Fan", SensorType.Fan, "/mock/board/fan", 1740);
var board = Hardware("Test Board", HardwareType.Motherboard, pwm, rpm);
var hub = new HardwareMonitorHub();
hub.Roots.AddRange(new[] { cpu, board });
var service = new FanControlService();
service.Discover(hub);
var target = service.Targets.Single();
var profile = new FanProfile { ControlId = target.Id };
var identity = new FanIdentity();
var settings = new AppSettings();
settings.FanProfiles.Add(profile);
settings.FanIdentities[target.Id] = identity;
var row = new FanProfileVm(target, profile, identity, () => { });

service.ReadLive(hub, settings);
service.Apply(hub, settings);
Check(target.SourceTemperatureC == 59, "Auto mode hid CPU temperature");
Check(row.LiveText.Contains("59") && row.SourceText.Contains("CPU Package"), "Auto row omitted reading or sensor source");
Check(row.SourceText.StartsWith("Monitor"), "Auto temperature was described as firmware's control source");
Check(softwareWrites == 0 && defaultWrites == 0, "Reading Auto touched a control never taken by software");
Check(target.ControlStatus == FanControlStatus.Firmware, "Auto was not reported as firmware control");

profile.Mode = FanMode.Constant;
profile.ConstantPercent = 45;
service.ReadLive(hub, settings);
service.Apply(hub, settings);
Check(target.SourceTemperatureC == 59, "Constant mode hid the monitored temperature");
Check(target.ControlStatus == FanControlStatus.Software && target.SoftwareControlled, "Accepted custom write was not reported as applied");
int written = softwareWrites;
service.ReadLive(hub, settings, readOnly: true);
Check(softwareWrites == written && target.SourceTemperatureC == 59, "Read-only monitoring wrote a control or hid temperature");
Check(target.ControlStatus == FanControlStatus.ReadOnly, "Read-only mode was shown as applied custom control");

Set(rpm, "Value", 0f);
service.ReadLive(hub, settings);
Check(target.RpmStatus == FanReadingStatus.Stopped && row.SpeedText == "0 RPM", "Zero RPM was treated as unavailable");
Check(row.ReadingStatusText.Contains("Stopped"), "Zero RPM has no explanation");
Set(rpm, "Value", null);
service.ReadLive(hub, settings);
Check(target.RpmStatus == FanReadingStatus.NoReading && row.SpeedText == "—", "Missing RPM was treated as stopped");
Check(row.ReadingStatusDetail.Contains("no reading"), "Missing RPM does not explain a temporary absent reading");
Set(board, "Sensors", new[] { pwm });
target.RpmSensor = null;
service.ReadLive(hub, settings);
Check(target.RpmStatus == FanReadingStatus.MissingSensor, "Absent tachometer did not differ from a missing reading");

// A missing preferred sensor must not beat a working alternative seen on the dashboard.
Set(package, "Value", null);
var core = Sensor("Core Max", SensorType.Temperature, "/mock/cpu/coremax", 62);
Set(cpu, "Sensors", new[] { package, core });
service.Discover(hub);
Check(ReferenceEquals(target, service.Targets.Single()), "Late temperature pairing rebuilt the target and discarded editing identity");
Check(target.CpuSensorId.EndsWith("coremax"), "Unavailable Package hid a valid Core Max sensor");
service.ReadLive(hub, settings);
Check(target.SourceTemperatureC == 62 && row.SourceText.Contains("Core Max"), "Late source did not refresh temperature and its name");
Set(package, "Value", 60f);
service.Discover(hub);
service.ReadLive(hub, settings);
Check(target.SourceTemperatureC == 60 && target.CpuSensorId.EndsWith("package"), "Preferred sensor did not resume when it became valid");

profile.Mode = FanMode.Sensor;
Set(package, "Value", null);
Set(core, "Value", float.NaN);
service.Discover(hub);
service.ReadLive(hub, settings);
service.Apply(hub, settings);
Check(target.SourceTemperatureC is null && target.TemperatureStatus == FanReadingStatus.NoReading, "Invalid temperature was treated as a valid reading");
Check(target.ControlStatus == FanControlStatus.SourceUnavailable && !target.SoftwareControlled, "Missing curve source was reported as successful custom control");
Check(defaultWrites > 0, "Missing curve source did not release an earlier software override");
Check(row.HasControlWarning && row.ReadingStatusDetail.Contains("Temperature"), "Source failure was invisible to bindings");

Set(package, "Value", 70f);
service.Discover(hub);
rejectSoftware = true;
service.Apply(hub, settings);
Check(target.ControlStatus == FanControlStatus.DriverRejected && !target.SoftwareControlled, "Driver rejection was presented as success");
rejectDefault = true;
service.Apply(hub, settings);
Check(target.ControlStatus == FanControlStatus.FirmwareReleaseRejected && row.HasControlWarning, "Failed firmware recovery was presented as normal");
int beforeRetry = defaultWrites;
rejectSoftware = false;
rejectDefault = false;
profile.Mode = FanMode.Auto;
service.Apply(hub, settings);
Check(defaultWrites == beforeRetry + 1 && target.ControlStatus == FanControlStatus.Firmware,
    "Failed release forgot control ownership instead of retrying");

// The hardware can leave old values in its sensor objects after an update throws. A failed
// hub must clear the displayed values and release overrides, never consume those stale values.
profile.Mode = FanMode.Constant;
service.Apply(hub, settings);
Check(target.SoftwareControlled, "Mock prerequisite: fixed speed was not applied");
written = softwareWrites;
int releases = defaultWrites;
hub.Available = false;
service.ReadLive(hub, settings);
service.Apply(hub, settings);
Check(softwareWrites == written && defaultWrites == releases + 1, "Failed hardware update reused a stale input or kept a software override");
Check(target.SourceTemperatureC is null && target.CurrentRpm is null && target.CurrentPercent is null,
    "A failed hub retained readings as if fresh");
Check(target.ControlStatus == FanControlStatus.SourceUnavailable && !target.SoftwareControlled,
    "A failed hub was displayed as applied control");
Check(ReferenceEquals(target, service.Targets.Single()), "A failed hub discarded the row rather than explaining unavailable readings");
hub.Available = true;
service.ReadLive(hub, settings);
service.Apply(hub, settings);
Check(target.SourceTemperatureC == 70 && target.SoftwareControlled, "A recovered update did not restore live readings and the selected mode");
profile.Mode = FanMode.Auto;
service.Apply(hub, settings);

// A GPU lacking a usable hotspot prefers its own core; no fallback can borrow another card.
var gpuControl = New<IControl>(new() { ["MinSoftwareValue"] = 30f, ["MaxSoftwareValue"] = 100f });
var gpuPwm = Sensor("GPU Fan", SensorType.Control, "/mock/gpu1/control", 30, gpuControl);
var gpuHot = Sensor("GPU Hot Spot", SensorType.Temperature, "/mock/gpu1/hotspot", null);
var gpuCore = Sensor("GPU Core", SensorType.Temperature, "/mock/gpu1/core", 38);
var gpu1 = Hardware("GPU One", HardwareType.GpuNvidia, gpuPwm, gpuHot, gpuCore);
var gpu2 = Hardware("GPU Two", HardwareType.GpuAmd,
    Sensor("GPU Hot Spot", SensorType.Temperature, "/mock/gpu2/hotspot", 99));
hub.Roots.AddRange(new[] { gpu1, gpu2 });
service.Discover(hub);
var gpuTarget = service.Targets.Single(t => t.Kind == "gpu");
service.ReadLive(hub, settings, readOnly: true);
Check(gpuTarget.SourceTemperatureC == 38 && gpuTarget.GpuSensorId.EndsWith("gpu1/core"), "GPU source borrowed another card or invalid hotspot");
Check(target.SourceTemperatureC == 70, "CPU fan Auto must ignore a hotter GPU");
var chassisPwm = Sensor("Chassis Fan", SensorType.Control, "/mock/board/chassis", 50, control);
Set(board, "Sensors", new[] { pwm, rpm, chassisPwm });
service.Discover(hub);
service.ReadLive(hub, settings, readOnly: true);
Check(service.Targets.Single(t => t.SensorName == "Chassis Fan").SourceTemperatureC == 70,
    "Board fan Auto must use CPU rather than compare CPU and GPU");
Set(board, "Sensors", new[] { pwm, rpm });
service.Discover(hub);
Set(gpu1, "Sensors", new[] { gpuPwm });
service.Discover(hub);
Check(gpuTarget.GpuSensorId.Length == 0, "A GPU without temperature sensors borrowed a different card's identity");
service.ReadLive(hub, settings, readOnly: true);
Check(gpuTarget.SourceTemperatureC is null && gpuTarget.SourceCategory == "gpu",
    "A missing GPU temperature must not silently switch to CPU");
settings.FanIdentities[target.Id] = System.Text.Json.JsonSerializer.Deserialize<FanIdentity>("{\"Name\":\"CPU Fan\",\"Source\":2}")!;
service.ReadLive(hub, settings, readOnly: true);
Check(target.SourceTemperatureC == 70, "Legacy GPU override must not change a CPU fan's source");
Set(package, "Value", null);
Set(core, "Value", null);
service.ReadLive(hub, settings, readOnly: true);
Check(target.SourceTemperatureC is null && target.SourceCategory == "cpu",
    "A missing selected CPU source must not silently switch to GPU");
Set(package, "Value", 70f);

// Driver reopen replaces handles. A pending release must follow its channel, rather than
// treating Close() as a successful release or forever retrying an invalid old handle.
profile.Mode = FanMode.Constant;
service.Apply(hub, settings);
rejectDefault = true;
hub.Available = false;
service.InvalidateReadings();
Check(target.ControlStatus == FanControlStatus.FirmwareReleaseRejected, "Failed invalidation claimed firmware control");
var replacementControl = New<IControl>(new() { ["MinSoftwareValue"] = 20f, ["MaxSoftwareValue"] = 100f });
Proxy(replacementControl).Calls["SetSoftware"] = _ => null;
int replacementDefaults = 0;
bool replacementRejectsDefault = true;
Proxy(replacementControl).Calls["SetDefault"] = _ =>
{
    replacementDefaults++;
    if (replacementRejectsDefault) throw new InvalidOperationException("Mock new handle refuses recovery");
    return null;
};
var replacementPwm = Sensor("CPU Fan", SensorType.Control, "/mock/board/control", 93, replacementControl);
Set(board, "Sensors", new[] { replacementPwm, rpm });
hub.Available = true;
profile.Mode = FanMode.Auto;
service.Discover(hub);
var replacementTarget = service.Targets.Single(t => t.Id == target.Id);
service.Apply(hub, settings);
Check(replacementDefaults == 1 && replacementTarget.ControlStatus == FanControlStatus.FirmwareReleaseRejected,
    "Reopened handle dropped a failed-release warning without accepting SetDefault");
replacementRejectsDefault = false;
service.Apply(hub, settings);
Check(replacementDefaults == 2 && replacementTarget.ControlStatus == FanControlStatus.Firmware && !replacementTarget.SoftwareControlled,
    "Recovery did not retry release through the replacement handle");

// A bad motherboard tree must not hide the healthy GPU, or consume old board values merely
// because at least one device kept the hub Available flag true.
profile.Mode = FanMode.Constant;
service.Apply(hub, settings);
Check(replacementTarget.SoftwareControlled, "Mock prerequisite: replacement override was not applied");
hub.Failed.Add(board);
service.Discover(hub);
Check(ReferenceEquals(replacementTarget, service.Targets.Single(t => t.Id == replacementTarget.Id)),
    "Partial failure removed the identifiable board fan");
int defaultsBeforePartial = replacementDefaults;
service.ReadLive(hub, settings);
service.Apply(hub, settings);
Check(replacementTarget.SourceTemperatureC is null && replacementTarget.CurrentPercent is null &&
    replacementTarget.CurrentRpm is null, "Healthy GPU caused stale motherboard readings to appear current");
Check(replacementDefaults == defaultsBeforePartial + 1 && !replacementTarget.SoftwareControlled,
    "Partial motherboard failure failed to release only its software override");
Check(gpuTarget.CurrentPercent == 30, "Partial motherboard failure hid the healthy GPU's reading");
hub.Failed.Clear();
service.Discover(hub);
service.ReadLive(hub, settings);
service.Apply(hub, settings);
Check(replacementTarget.SoftwareControlled && replacementTarget.SourceTemperatureC == 70,
    "A recovered motherboard failed to refresh its original row and source");

// The CPU tree can fail while its board fan succeeds: old Package temperature must not be
// found as a curve source. This isolated hub has no alternate GPU temperature to follow.
var cpuOnlyHub = new HardwareMonitorHub();
cpuOnlyHub.Roots.AddRange(new[] { cpu, board });
var cpuOnlyService = new FanControlService();
cpuOnlyService.Discover(cpuOnlyHub);
profile.Mode = FanMode.Sensor;
cpuOnlyService.ReadLive(cpuOnlyHub, settings);
cpuOnlyService.Apply(cpuOnlyHub, settings);
cpuOnlyHub.Failed.Add(cpu);
cpuOnlyService.Discover(cpuOnlyHub);
cpuOnlyService.ReadLive(cpuOnlyHub, settings);
cpuOnlyService.Apply(cpuOnlyHub, settings);
var cpuOnlyTarget = cpuOnlyService.Targets.Single();
Check(cpuOnlyTarget.SourceTemperatureC is null && cpuOnlyTarget.ControlStatus == FanControlStatus.SourceUnavailable,
    "Healthy board fan kept following a failed CPU's old Package value");

Console.WriteLine($"PASS: {checks} fan monitoring / source / control-state checks; all controls are mocks.");

static MockProxy Proxy<T>(T value) where T : class => (MockProxy)(object)value;
static T New<T>(Dictionary<string, object?> values) where T : class
{
    T item = DispatchProxy.Create<T, MockProxy>();
    Proxy(item).Values = values;
    return item;
}
static void Set<T>(T value, string property, object? data) where T : class => Proxy(value).Values[property] = data;
static ISensor Sensor(string name, SensorType type, string id, float? value, IControl? control = null) =>
    New<ISensor>(new() { ["Name"] = name, ["SensorType"] = type, ["Identifier"] = new Identifier(id.Trim('/').Split('/')),
        ["Value"] = value, ["Control"] = control });
static IHardware Hardware(string name, HardwareType type, params ISensor[] sensors) =>
    New<IHardware>(new() { ["Name"] = name, ["HardwareType"] = type, ["Sensors"] = sensors, ["SubHardware"] = Array.Empty<IHardware>() });

public class MockProxy : DispatchProxy
{
    public Dictionary<string, object?> Values { get; set; } = new();
    public Dictionary<string, Func<object?[]?, object?>> Calls { get; } = new();
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) throw new InvalidOperationException("Missing mocked method");
        if (Calls.TryGetValue(targetMethod.Name, out var action)) return action(args);
        if (targetMethod.Name.StartsWith("get_"))
        {
            if (Values.TryGetValue(targetMethod.Name[4..], out var value)) return value;
            return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
        throw new InvalidOperationException("Unexpected mocked call: " + targetMethod.Name);
    }
}
