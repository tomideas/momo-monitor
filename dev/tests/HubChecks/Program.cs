using System.Reflection;
using LibreHardwareMonitor.Hardware;
using StatusMonitor.Sensors;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

bool rejectUpdate = false;
int updates = 0;
IHardware fake = DispatchProxy.Create<IHardware, HardwareProxy>();
var proxy = (HardwareProxy)(object)fake;
proxy.Update = () => { updates++; if (rejectUpdate) throw new InvalidOperationException("Mock update failure"); };
Computer.NextHardware = new[] { fake };
var hub = new HardwareMonitorHub();
Check(!hub.Available && !hub.Update(), "Unopened hub claims live hardware");
hub.Open();
Check(hub.Available && Computer.Opens == 1, "Mock hardware did not open");
Check(hub.Update() && updates == 1 && hub.Error is null, "Successful update is not readable");
Check(hub.Find(HardwareType.Cpu) == fake && hub.All(HardwareType.Cpu).Single() == fake,
    "Successful update lost hardware discovery");

rejectUpdate = true;
Check(!hub.Update() && !hub.Available && hub.Error == "Mock update failure", "Failed update was exposed as successful / available");
Check(hub.Find(HardwareType.Cpu) is null && !hub.All(HardwareType.Cpu).Any(), "Failed update exposed the stale hardware tree");
rejectUpdate = false;
Check(hub.Update() && hub.Available && hub.Error is null, "Later successful sample did not clear the error");
Check(hub.Find(HardwareType.Cpu) == fake, "Recovered sensors remained unavailable");

var healthyGpu = DispatchProxy.Create<IHardware, HardwareProxy>();
((HardwareProxy)(object)healthyGpu).Type = HardwareType.GpuNvidia;
Computer.NextHardware = new[] { fake, healthyGpu };
hub.Open();
rejectUpdate = true;
Check(hub.Update() && hub.Available && hub.HasFailures, "One failed device must not hide a healthy GPU");
Check(hub.Find(HardwareType.Cpu) is null && hub.Find(HardwareType.GpuNvidia) == healthyGpu,
    "Partial update exposed failed CPU or lost healthy GPU");
Check(!hub.IsCurrent(fake) && hub.IsCurrent(healthyGpu), "Device freshness was not isolated");
Check(hub.Discovered(HardwareType.Cpu).Single() == fake, "Failed device identity was lost");
rejectUpdate = false;
Check(hub.Update() && !hub.HasFailures && hub.Error is null, "Recovery did not clear partial failure");

int priorOpens = Computer.Opens, priorCloses = Computer.Closes;
hub.Open();
Check(Computer.Opens == priorOpens + 1 && Computer.Closes == priorCloses + 1 && hub.Available, "Retry did not close and reopen the hardware hub");
Computer.RejectOpen = true;
hub.Open();
Check(!hub.Available && hub.Error?.Contains("Mock open failure") == true, "Failed reopen was presented as available");
Check(!hub.Update() && hub.Find(HardwareType.Cpu) is null, "Failed reopen left old sensor objects active");
Computer.RejectOpen = false;
hub.Open();
Check(hub.Update() && hub.Error is null && hub.Available, "A retry after open failure could not recover");
hub.Dispose();
Check(!hub.Available && hub.Find(HardwareType.Cpu) is null, "Closed hub still exposed readings");
Console.WriteLine($"PASS: {checks} hub update / stale-data / reopen checks; Computer is a test-only mock.");

public class HardwareProxy : DispatchProxy
{
    public HardwareType Type { get; set; } = HardwareType.Cpu;
    public Action Update { get; set; } = () => { };
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        "get_HardwareType" => Type,
        "get_SubHardware" => Array.Empty<IHardware>(),
        "Update" => DoUpdate(),
        _ => throw new InvalidOperationException("Unexpected mocked method: " + targetMethod?.Name),
    };
    private object? DoUpdate() { Update(); return null; }
}
