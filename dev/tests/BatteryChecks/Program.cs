using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using StatusMonitor.Models;
using StatusMonitor.Sensors;
using StatusMonitor.Services;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}
BatterySensor.NativeReading Pack(int rate, uint state = 2, uint capabilities = 0x80000000,
    uint full = 50_000, uint remaining = 25_000, bool ok = true) =>
    new(capabilities, full, remaining, state, rate, ok);
BatteryReading Interpret(BatterySensor.NativeReading[] batteries, bool? onAc = false,
    bool complete = true, double? percent = 51) =>
    BatterySensor.Interpret(batteries, onAc, percent, true, complete, null);

var discharge = Interpret([Pack(-12_345)]);
Check(discharge.IsPresent && discharge.Status == "Discharging", "System battery must be recognized.");
Check(discharge.DischargeWatts == 12.345 && discharge.ChargeWatts == null, "Signed negative mW is discharge W only.");
Check(discharge.ChargePercent == 50, "API capacity/full-capacity defines charge percentage.");
var charge = Interpret([Pack(17_650, 5)], true);
Check(charge.ChargeWatts == 17.65 && charge.DischargeWatts == null, "Charging W must not become machine consumption.");
Check(charge.Status == "Charging" && charge.IsOnAcPower == true, "AC and charging are preserved.");
Check(Interpret([Pack(int.MinValue)]).DischargeWatts == null, "Unknown signed rate must stay missing.");
Check(Interpret([Pack(-20_000, capabilities: 0xC0000000)]).DischargeWatts == null, "Relative rates cannot be converted to W.");
Check(Interpret([Pack(-20_000, capabilities: 0xC0000000)]).Error?.Contains("relative") == true, "Relative units have an explanation.");
var multi = Interpret([Pack(-10_000), Pack(-20_000, full: 25_000, remaining: 25_000)]);
Check(multi.DischargeWatts == 30, "Same-direction complete multi-battery rates aggregate.");
Check(Math.Abs(multi.ChargePercent!.Value - 200.0 / 3) < 0.00001, "Absolute multi-pack capacity is weighted.");
Check(Interpret([Pack(-10_000), Pack(int.MinValue)]).DischargeWatts == null, "A missing pack rate must not under-report whole-machine consumption.");
Check(Interpret([Pack(-10_000), Pack(-10_000, ok: false)]).DischargeWatts == null, "Failed status blocks multi-pack aggregation.");
Check(Interpret([Pack(-10_000)], complete: false).DischargeWatts == null, "Incomplete device enumeration cannot claim a whole-machine rate.");
var mixed = Interpret([Pack(-10_000), Pack(2_000, 4)], null);
Check(mixed.Status == "Mixed" && mixed.DischargeWatts == null && mixed.ChargeWatts == null,
    "Mixed-direction packs cannot masquerade as net machine power.");
var ups = Interpret([Pack(-10_000, capabilities: 0xA0000000)]);
Check(!ups.IsPresent && ups.Status == "NoBattery", "UPS devices do not classify desktops as battery notebooks.");
Check(!BatterySensor.Interpret([], true, null, false, true, null).IsPresent, "No battery stays absent.");
Check(Interpret([], complete: false).Status == "Unavailable", "System-only fallback identifies missing rate support.");
Check(Interpret([Pack(0)]).DischargeWatts == 0, "A valid reported zero rate is distinct from missing.");
Check(Interpret([Pack(5_000)]).DischargeWatts == null, "A positive rate is not discharge even with an inconsistent state.");
Check(Interpret([Pack(-5_000)], true).DischargeWatts == null, "Contradictory AC state does not drive consumption.");
Check(Interpret([Pack(-5_000, remaining: uint.MaxValue)], percent: 101).ChargePercent == null, "Unknown capacity and invalid percentage remain missing.");
Check(Interpret([Pack(-5_000, full: 100, remaining: 150)]).ChargePercent == 100, "Firmware overfull capacity is bounded to 100%.");
Check(BatterySensor.ClassifyPlatform(true, "Unknown") == "Portable", "A system battery identifies a portable machine.");
Check(BatterySensor.ClassifyPlatform(false, "Unknown") == "Unknown", "Missing evidence does not assume desktop.");
Check(BatterySensor.ClassifyChassis([31]) == "Portable", "Convertible chassis is portable.");
Check(BatterySensor.ClassifyChassis([3]) == "Desktop", "Desktop chassis is positively identified.");
Check(BatterySensor.ClassifyChassis([1, 2]) == "Unknown", "Other/unknown chassis remains unknown.");
foreach (var (name, size) in new[] { ("SystemPowerStatus", 12), ("BatteryQueryInformation", 12),
    ("BatteryInformation", 36), ("BatteryWaitStatus", 20), ("BatteryStatus", 16) })
{
    var type = typeof(BatterySensor).GetNestedType(name, BindingFlags.NonPublic)!;
    Check(Marshal.SizeOf(type) == size, $"{name} must match the Windows SDK ABI.");
}

string reportPath = Path.Combine(Path.GetTempPath(), "Momo-BatteryChecks-" + Guid.NewGuid().ToString("N") + ".csv");
var originalCulture = CultureInfo.CurrentCulture;
try
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
    DateTimeOffset sampled = new(2026, 10, 7, 9, 8, 7, TimeSpan.FromHours(8));
    SensorReportService.Export(reportPath,
        [new SensorReading { DeviceId = "=bad", DeviceName = "CPU,中文", Id = "/cpu/power/0", Name = " @unsafe",
          Type = "Power", Unit = "W", Scope = "package", Value = 12.5, Minimum = null, Maximum = double.NaN, State = "valid" },
         new SensorReading { DeviceId = "safe", Name = "Core \"Max\"", Type = "Temperature", Unit = "°C", Value = -1.5, State = "valid" }],
        "Unknown", charge, sampled, "Partial sample; GPU update failed.");
    string report = File.ReadAllText(reportPath);
    Check(report.Contains("SampleTimeUtc,2026-10-07T01:08:07.0000000+00:00"), "The report records sample time rather than export time.");
    Check(report.Contains("ExportTimeUtc,"), "Export time is separate metadata.");
    Check(report.Contains("Partial sample; GPU update failed."), "Partial sample reasons survive export.");
    Check(report.Contains("'" + "=bad") && report.Contains("' @unsafe"), "Formula-like text is protected even with leading whitespace.");
    Check(report.Contains("\"CPU,中文\"") && report.Contains("\"Core \"\"Max\"\"\""), "CSV commas and quotes round-trip.");
    Check(report.Contains(",12.5,,,valid") && report.Contains(",-1.5,,,valid"), "Numbers are invariant and finite; negative numerical values are not converted to text.");
    Check(report.Contains("LibraryBuildSHA256,C81F48135F56BFD22E5273B61D8265D1E75C4E7C9964845FD56AF27286EE07AA"), "Report fingerprints the shipped LHM build.");
    Check(report.Contains("BatteryChargeWatts,17.649999999999999") || report.Contains("BatteryChargeWatts,17.65"), "Battery charging has its own column.");
    Check(!report.Contains(Environment.MachineName) && !report.Contains(Environment.UserName), "No machine/user identity is emitted.");
    Check(File.ReadAllBytes(reportPath).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }), "UTF-8 BOM supports Chinese spreadsheet imports.");
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
    File.Delete(reportPath);
}
Console.WriteLine($"BatteryChecks: {checks} assertions passed (no hardware queries or writes).");
