using System.Text.Json;
using StatusMonitor.Power;
using StatusMonitor.Services;
using StatusMonitor.Settings;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
bool Near(double value, double expected) => Math.Abs(value - expected) < 0.0000001;
var t = DateTimeOffset.Parse("2026-10-07T12:00:00+08:00");
const string measured = "measured:cpu-gpu", estimated = "estimated:components";

foreach (string name in new[] { "NVIDIA GeForce RTX 4070 Laptop GPU", "NVIDIA GeForce RTX 4060 Notebook GPU", "NVIDIA Quadro P1000 Mobile", "NVIDIA GeForce RTX 3080 Max-Q", "AMD Radeon RX 6600M", "Intel Arc A770M" })
    Check(PowerModel.LookupGpuTdp(name) == 0, $"Mobile GPU must not match desktop: {name}");
foreach (string name in new[] { "Intel Core i7-14700HX", "Intel Core i9-13900H", "AMD Ryzen 7 5800H", "Unknown future CPU", "", "Intel i9-14900K Laptop" })
    Check(PowerModel.LookupCpuTdp(name) == 0, $"Unknown/mobile CPU must have unknown rating: {name}");
Check(PowerModel.LookupCpuTdp("Intel Core i9-13900KS") == 125 && PowerModel.LookupCpuTdp("Intel Core i7-12700KF") == 125,
    "Desktop KS/KF model suffixes remain supported");
Check(PowerModel.LookupCpuTdp("Intel Core i5-12400F") == 65,
    "A desktop F suffix remains supported without accepting mobile H/HX suffixes");
Check(PowerModel.LookupGpuTdp("NVIDIA RTX 4070 Ti") == 285 && PowerModel.LookupGpuTdp("NVIDIA RTX 4070") == 200,
    "Desktop longest model match remains supported");
foreach (string name in new[] { "Intel(R) Arc(TM) Graphics", "Intel(R) Arc(TM) 140V GPU", "Intel Arc 130T Graphics", "Intel(R) Graphics", "AMD Radeon 8050S Graphics", "AMD Radeon 8060S", "AMD Radeon 860M", "AMD Radeon(TM) Graphics" })
    Check(GpuSelection.IsKnownIntegrated(name), $"Known integrated GPU: {name}");
foreach (string name in new[] { "Intel Arc A770", "Intel Arc B580", "Intel Arc Pro B50", "Intel Arc A370M", "Intel Iris Xe MAX Graphics", "AMD Radeon RX 6600M", "AMD Radeon RX 7900 XTX", "Radeon RX Vega 56" })
    Check(!GpuSelection.IsKnownIntegrated(name), $"Discrete board must stay discrete: {name}");

var integrator = new EnergyIntegrator();
Check(integrator.Sample(100, t, 100, measured, 20).EnergyWh == 0, "First sample establishes baseline only");
var result = integrator.Sample(110, t.AddSeconds(10), 200, measured, 20);
Check(Near(result.EnergyWh, 150 * 10 / 3600.0) && result.MonitoredSeconds == 10 && result.MissingSeconds == 0,
    "Consecutive readings use trapezoid integration");
Check(result.Segments.Count == 1 && result.Segments[0].Day == DateOnly.FromDateTime(t.DateTime), "Local date owns same-day interval");
result = integrator.Sample(120, t.AddSeconds(20), 0, measured, 20);
Check(Near(result.EnergyWh, 100 * 10 / 3600.0), "Valid zero is the trapezoid endpoint");
result = integrator.Sample(130, t.AddSeconds(30), 0, measured, 20);
Check(result.EnergyWh == 0 && result.MonitoredSeconds == 10, "Two valid zero readings still record monitored seconds");
result = integrator.Sample(140, t.AddSeconds(40), null, measured, 20);
Check(result.EnergyWh == 0 && result.MissingSeconds == 10, "Missing endpoint never integrates the preceding power");
result = integrator.Sample(150, t.AddSeconds(50), 100, measured, 20);
Check(result.EnergyWh == 0 && result.MissingSeconds == 10, "Recovery first re-establishes power baseline");
result = integrator.Sample(160, t.AddSeconds(60), 100, measured, 20);
Check(Near(result.EnergyWh, 1000.0 / 3600), "Next consecutive valid recovery interval integrates");
result = integrator.Sample(170, t.AddSeconds(70), 100, estimated, 20);
Check(result.EnergyWh == 0 && result.MissingSeconds == 10 && result.Basis == estimated, "Changed scope resets integration boundary");
result = integrator.Sample(180, t.AddSeconds(80), 100, estimated, 20);
Check(result.MonitoredSeconds == 10, "New scope integrates its own consecutive samples");
result = integrator.Sample(280, t.AddSeconds(180), 100, estimated, 20);
Check(result.EnergyWh == 0 && result.MissingSeconds == 100, "Long awake gap is unknown rather than invented consumption");
result = integrator.Sample(281, t.AddSeconds(180).AddHours(8), 100, estimated, 20);
Check(result.EnergyWh == 0 && result.MissingSeconds == 1, "Sleep wall gap never creates 0.8 kWh or counts sleeping seconds as monitored");
integrator.Reset();
Check(integrator.Sample(300, t, 100, measured, 20).Segments.Count == 0, "Explicit reset drops the prior baseline");
result = integrator.Sample(299, t.AddSeconds(1), 100, measured, 20);
Check(result.EnergyWh == 0 && result.MonitoredSeconds == 0 && result.MissingSeconds == 0, "Backwards awake clock resets without negative coverage");
foreach (double? watts in new double?[] { double.NaN, double.PositiveInfinity, -1, null })
{
    integrator.Reset(); integrator.Sample(0, t, 100, measured, 20);
    Check(integrator.Sample(1, t.AddSeconds(1), watts, measured, 20).MissingSeconds == 1, "Invalid watts invalidate interval");
}
integrator.Reset(); integrator.Sample(0, t, 100, measured, 20);
Check(integrator.Sample(1, t.AddSeconds(-1), 100, measured, 20).EnergyWh == 0, "Wall clock correction cannot create energy");
integrator.Reset(); integrator.Sample(0, t, 100, measured, 20);
Check(integrator.Sample(1, t.AddSeconds(1).ToOffset(TimeSpan.FromHours(9)), 100, measured, 20).EnergyWh == 0, "Time-zone change re-establishes local day boundary");

var midnight = DateTimeOffset.Parse("2026-10-07T23:59:55+08:00");
integrator.Reset(); integrator.Sample(0, midnight, 0, measured, 20);
result = integrator.Sample(10, midnight.AddSeconds(10), 100, measured, 20);
Check(result.Segments.Count == 2 && result.Segments[0].Day == new DateOnly(2026, 10, 7) && result.Segments[1].Day == new DateOnly(2026, 10, 8),
    "Midnight splits the interval between the two real local days");
Check(Near(result.Segments[0].EnergyWh, 25 * 5 / 3600.0) && Near(result.Segments[1].EnergyWh, 75 * 5 / 3600.0),
    "Midnight split preserves the trapezoid slope, not equal half energy");
Check(Near(result.Segments.Sum(s => s.EnergyWh), result.EnergyWh) && result.MonitoredSeconds == 10, "Split preserves total energy and coverage");

var day = new DateOnly(2026, 10, 7);
var history = new EnergyHistory(persist: false);
history.Add(day, 0, 10, 0, measured);
history.Add(day, 1, 20, 5, estimated);
history.Add(day.AddDays(-1), 2, 30, 0, measured);
var coverage = history.Coverage(EnergyPeriod.Today, day);
Check(history.Sum(EnergyPeriod.Today, day) == 1 && coverage.MonitoredSeconds == 30 && coverage.MissingSeconds == 5,
    "Coverage records zero watts and excludes yesterday from today");
Check(coverage.MeasuredSeconds == 10 && coverage.EstimatedSeconds == 20 && !coverage.LegacyCoverageUnknown,
    "Coverage separately identifies measured and estimated scope seconds");
Check(coverage.BasisSeconds[measured] == 10 && coverage.BasisSeconds[estimated] == 20, "Coverage preserves exact source scope groups");
history.Add(day.AddDays(-2), 3);
Check(history.Coverage(EnergyPeriod.Week, day).LegacyCoverageUnknown, "Old Add API cannot claim known monitored duration");
history.Add(day.AddDays(-2), 0, 10, 0, measured);
Check(history.Coverage(EnergyPeriod.Week, day).LegacyCoverageUnknown, "New observations do not erase unknown legacy coverage");
history.Add(day, double.NaN, 10, 0, measured); history.Add(day, 1, -10, 0, measured);
Check(history.Sum(EnergyPeriod.Today, day) == 1, "Nonfinite energy and negative duration are rejected");
history.Clear();
Check(history.Sum(EnergyPeriod.All, day) == 0 && history.Coverage(EnergyPeriod.All, day).MonitoredSeconds == 0,
    "Reset clears both energy and coverage");

string baseDir = Path.GetFullPath(AppContext.BaseDirectory);
string testDir = Path.GetFullPath(Path.Combine(baseDir, ".powerchecks-" + Guid.NewGuid().ToString("N")));
if (!testDir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe test directory");
Directory.CreateDirectory(testDir);
AppSettings.DataDir = testDir;
string dataPath = Path.Combine(testDir, "energy-history.json");
try
{
    File.WriteAllText(dataPath, JsonSerializer.Serialize(new { days = new Dictionary<string, double> { [day.ToString("yyyy-MM-dd")] = 2.5 } }));
    string firstMachine = new('A', 64), secondMachine = new('B', 64);
    var saved = new EnergyHistory(machineId: firstMachine);
    Check(saved.Sum(EnergyPeriod.Today, day) == 2.5 && saved.Coverage(EnergyPeriod.Today, day).LegacyCoverageUnknown,
        "Legacy days-only JSON loads without invented coverage");
    saved.Add(day, 0.5, 60, 5, measured); saved.Save();
    var reloaded = new EnergyHistory(machineId: secondMachine);
    Check(reloaded.Sum(EnergyPeriod.Today, day) == 3 && reloaded.Coverage(EnergyPeriod.Today, day).MeasuredSeconds == 60,
        "Energy and coverage both survive atomic persistence");
    reloaded.Add(day, 0, 10, 0, measured); reloaded.Save();
    Check(reloaded.Coverage(EnergyPeriod.Today, day).MultipleMachines, "Copying portable data preserves history while showing mixed machine markers");
    string json = File.ReadAllText(dataPath);
    Check(json.Contains(firstMachine) && json.Contains(secondMachine) && !json.Contains(Environment.MachineName),
        "History stores only supplied hash markers, never machine names");
    File.WriteAllText(dataPath, "broken json");
    var recovered = new EnergyHistory(machineId: firstMachine);
    Check(recovered.Sum(EnergyPeriod.Today, day) == 3 && recovered.Coverage(EnergyPeriod.Today, day).MeasuredSeconds == 60,
        "Backup recovers both energy and known monitored duration together");
}
finally
{
    // Delete only the generated, checked test child; never any application's real data.
    if (testDir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(testDir).StartsWith(".powerchecks-", StringComparison.Ordinal))
        Directory.Delete(testDir, recursive: true);
}
Console.WriteLine($"PowerChecks: {checks} checks passed");
