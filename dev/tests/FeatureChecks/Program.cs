using System.Text.Json;
using StatusMonitor.Models;
using StatusMonitor.Power;
using StatusMonitor.Services;
using StatusMonitor.Settings;
using StatusMonitor.ViewModels;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
var t = DateTimeOffset.Parse("2026-09-12T00:00:00Z");
var engine = new AlertEngine();
int Tick(int seconds, double? value, bool below = false) => engine.Evaluate(t.AddSeconds(seconds), new[] { new AlertSignal("sensor", "Test", value, 90, "%", below) }, 3, 10).Count;
Check(Tick(0, 95) == 0, "First threshold crossing must not notify");
Check(Tick(1, 95) == 0 && Tick(2, 95) == 0, "Hold period must be respected");
Check(Tick(3, 95) == 1, "Sustained excursion should notify");
for (int i = 4; i <= 15; i++) Check(Tick(i, 95) == 0, "No repeat notification during same excursion");
Check(Tick(16, 30) == 0, "Recovery should not emit breach");
Check(Tick(17, 95) == 0 && Tick(18, 95) == 0 && Tick(19, 95) == 0 && Tick(20, 95) == 1, "New excursion after cooldown can notify");
Tick(21, 0); Tick(22, 95); Tick(23, 95); Tick(24, 95);
Check(Tick(25, 95) == 0, "Cooldown also applies across recoveries");
for (int i = 26; i < 30; i++) Check(Tick(i, 95) == 0, "Cooldown should not send early");
Check(Tick(30, 95) == 1, "Pending sustained incident can send when cooldown expires");
engine.Reset(); Tick(0, 95); Tick(1, null); Tick(2, 95); Tick(3, 95);
Check(Tick(4, 95) == 0 && Tick(5, 95) == 1, "Missing values reset sustained duration");
engine.Reset(); Tick(0, 95);
engine.Evaluate(t.AddSeconds(1), Array.Empty<AlertSignal>(), 3, 10);
Check(Tick(2, 95) == 0 && Tick(3, 95) == 0 && Tick(4, 95) == 0 && Tick(5, 95) == 1, "Disappearing sensors reset pending state");
engine.Reset(); Tick(0, 95);
Check(Tick(20, 95) == 0, "Sleep / long sample gap must not count as continuous high readings");
engine.Reset(); Check(Tick(0, double.NaN) == 0 && Tick(1, double.PositiveInfinity) == 0, "Nonfinite values must not alert");
engine.Reset(); Tick(0, 5, true); Tick(1, 5, true); Tick(2, 5, true);
Check(Tick(3, 5, true) == 1, "Low disk free space uses below threshold semantics");

var snapshot = new Snapshot { Gpus = new() {
    new() { Id = "integrated", IsIntegrated = true, MemoryTotalMb = 32000 },
    new() { Id = "dedicated", MemoryTotalMb = 8000 } }, GpuNames = new() { "iGPU", "dGPU" } };
Check(GpuSelection.IsKnownIntegrated("Intel(R) UHD Graphics 730"), "Known Intel iGPU should be recognized");
Check(GpuSelection.IsKnownIntegrated("AMD Radeon 780M"), "Known AMD iGPU should be recognized");
Check(!GpuSelection.IsKnownIntegrated("Intel Arc A770") && !GpuSelection.IsKnownIntegrated("Intel Iris Xe MAX Graphics"), "Intel discrete GPUs must not be hidden as integrated");
Check(!GpuSelection.IsKnownIntegrated("NVIDIA RTX 4090"), "Unknown or discrete GPU must stay visible");
Check(GpuSelection.Visible(snapshot, "", false).SequenceEqual(new[] { 1, 0 }), "Auto must prefer discrete rather than larger shared memory");
Check(GpuSelection.Visible(snapshot, "integrated", false).SequenceEqual(new[] { 0, 1 }), "Explicit GPU selection must override auto preference");
Check(GpuSelection.Visible(snapshot, "integrated", true).SequenceEqual(new[] { 1 }), "Hide integrated GPU when dedicated exists");
Check(GpuSelection.Visible(snapshot, "disconnected", false)[0] == 1, "Missing preferred GPU must fall back safely");
snapshot.Gpus.Reverse(); snapshot.GpuNames.Reverse();
Check(GpuSelection.Visible(snapshot, "integrated", false)[0] == 1, "Selection must follow identity after enumeration changes");
snapshot.Gpus.RemoveAt(0); snapshot.GpuNames.RemoveAt(0);
Check(GpuSelection.Visible(snapshot, "", true).Count == 1, "An integrated-only computer must retain its sole GPU");
snapshot.Gpus.Clear(); snapshot.GpuNames.Clear();
Check(GpuSelection.Visible(snapshot, "", true).Count == 0, "No GPU is a supported state");

var history = new TelemetryHistory();
for (int i = 0; i <= 1800; i++) history.Add(t.AddSeconds(i), new Dictionary<string, double?> { ["cpu"] = i % 100, ["gpuA"] = 20, ["gpuB"] = 80 });
Check(history.Count == 901, "History must stay bounded to 15 minutes including boundary");
Check(history.Series("cpu", 60, t.AddSeconds(1800)).Length == 61, "60-second range should include only relevant timestamps");
Check(history.Series("gpuA", 60, t.AddSeconds(1800)).All(p => p.Value == 20) && history.Series("gpuB", 60, t.AddSeconds(1800)).All(p => p.Value == 80), "Switching GPUs must not mix their histories");
history.Add(t.AddSeconds(1801), new Dictionary<string, double?>());
Check(history.Series("cpu", 60, t.AddSeconds(1801))[^1].Value is null, "Missing metric must leave a gap, not zero");
history.Add(t.AddSeconds(1802), new Dictionary<string, double?> { ["cpu"] = 0 });
Check(history.Series("cpu", 60, t.AddSeconds(1802))[^1].Value == 0, "Real zero must remain a valid reading");
history.Add(t, new Dictionary<string, double?> { ["cpu"] = 10 });
Check(history.Count == 1, "Backward clock changes must not corrupt timeline");
var mutable = new Dictionary<string, double?> { ["cpu"] = 12 };
history.Add(t.AddSeconds(1), mutable); mutable["cpu"] = 99;
Check(history.Series("cpu", 60, t.AddSeconds(1))[^1].Value == 12, "History snapshots must not retain mutable input references");
var oldSettings = JsonSerializer.Deserialize<AppSettings>("{\"Language\":\"en\",\"PricePerKwh\":0.42,\"ProcessCount\":5}")!;
// MiniTopmost defaults off now — a panel that plants itself over every other window is the
// user's call to make. A file written before the option existed must land on the new default,
// as must one written before the refresh interval existed.
Check(oldSettings.Language == "en" && oldSettings.PricePerKwh == 0.42 && oldSettings.ProcessCount == 5
      && !oldSettings.MiniTopmost && oldSettings.EffectiveRefreshSeconds == 2, "Old settings must keep existing preferences and gain defaults");
// A file written before the close prompt existed must arrive asking, not silently keeping
// whatever the tray checkbox said. The default that loses nothing is the one that asks.
Check(oldSettings.AskOnClose, "Settings from before the close prompt must still ask on close");
var quiet = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(
    new AppSettings { AskOnClose = false, RunInTray = true }))!;
Check(!quiet.AskOnClose && quiet.RunInTray,
    "A remembered close choice must survive a restart, or the prompt returns after being dismissed");
oldSettings.BackgroundTheme = "violet"; oldSettings.PreferredGpuId = "dedicated"; oldSettings.StartInMiniMode = true; oldSettings.TrendSeconds = 900; oldSettings.AlertHoldSeconds = 23;
var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(oldSettings))!;
Check(restored.BackgroundTheme == "violet" && restored.PreferredGpuId == "dedicated" && restored.StartInMiniMode && restored.TrendSeconds == 900 && restored.AlertHoldSeconds == 23, "New preferences must round-trip");
// ---- fan curve ----
// This maths decides how fast a fan spins, so it is pinned down here rather than trusted.
// The tab configures a ramp between two temperatures; the speeds are the fan's own range.
var ramp = FanCurve.Ramp(50, 90, 34, 100);
Check(FanCurve.Evaluate(ramp, 30) == 34, "Below the start temperature the fan stays at its minimum");
Check(FanCurve.Evaluate(ramp, 95) == 100, "At and above the max temperature the fan is at full speed");
Check(Math.Abs(FanCurve.Evaluate(ramp, 70) - 67) < 0.001, "Halfway up the ramp is halfway through the fan's range");
// Tightening the window is what makes a ramp aggressive — the case the Fans tab is built for.
var tight = FanCurve.Ramp(55, 80, 34, 100);
Check(FanCurve.Evaluate(tight, 70) > FanCurve.Evaluate(ramp, 70), "A narrower window must drive harder at the same temperature");
Check(Math.Abs(FanCurve.Evaluate(tight, 70) - 73.6) < 0.1, "55-80C reaches ~74% at 70C on a fan with a 34% floor");
// An inverted or degenerate window must not divide by zero.
// A zero-width window becomes a step at that temperature: minimum below it, maximum above.
Check(FanCurve.Evaluate(FanCurve.Ramp(70, 70, 34, 100), 69) == 34, "Below a zero-width ramp the fan stays at its minimum");
Check(FanCurve.Evaluate(FanCurve.Ramp(70, 70, 34, 100), 71) == 100, "Above a zero-width ramp the fan is at full speed, with no division by zero");
Check(FanCurve.Evaluate(FanCurve.Ramp(80, 50, 34, 100), 90) == 100, "An inverted ramp must still be evaluable");

var curve = new List<FanPoint>
{
    new() { TemperatureC = 45, Percent = 35 }, new() { TemperatureC = 60, Percent = 50 },
    new() { TemperatureC = 70, Percent = 70 }, new() { TemperatureC = 75, Percent = 80 },
    new() { TemperatureC = 85, Percent = 100 },
};
Check(FanCurve.Evaluate(curve, 20) == 35, "Below the first node the curve must stay flat, not extrapolate down");
Check(FanCurve.Evaluate(curve, 200) == 100, "Above the last node the curve must stay flat, not extrapolate up");
Check(FanCurve.Evaluate(curve, 45) == 35 && FanCurve.Evaluate(curve, 70) == 70, "Nodes must return their own value");
Check(Math.Abs(FanCurve.Evaluate(curve, 65) - 60) < 0.001, "Between nodes must interpolate linearly (65C sits midway between 60/50 and 70/70)");
Check(FanCurve.Evaluate(new List<FanPoint>(), 70) == 0, "An empty curve must not throw");
// Points may be entered in any order; evaluation sorts them.
var jumbled = new List<FanPoint> { new() { TemperatureC = 80, Percent = 90 }, new() { TemperatureC = 40, Percent = 30 } };
Check(FanCurve.Evaluate(jumbled, 40) == 30 && FanCurve.Evaluate(jumbled, 80) == 90, "Unsorted points must still evaluate correctly");

var state = new FanCurveState();
Check(state.Next(curve, 70, 3, 85) == 70, "First reading applies the curve directly");
Check(state.Next(curve, 68, 3, 85) == 70, "A 2C drop is inside the 3C hysteresis and must hold the speed");
Check(state.Next(curve, 67, 3, 85) < 70, "A 3C drop is outside the hysteresis and must step the speed down");
state.Reset();
Check(state.Next(curve, 60, 3, 85) == 50, "Reset must clear the applied temperature");
Check(state.Next(curve, 62, 3, 85) > 50, "Rising temperature must respond immediately, never lag behind hysteresis");
Check(state.Next(curve, 90, 3, 85) == 100, "The failsafe temperature must force 100%");
Check(state.Next(curve, 86, 3, 85) == 100, "The failsafe must not be released by hysteresis while still above it");
// A curve whose own top node is below the failsafe must still reach 100% in an emergency.
var lowCurve = new List<FanPoint> { new() { TemperatureC = 40, Percent = 20 }, new() { TemperatureC = 70, Percent = 40 } };
var lowState = new FanCurveState();
Check(lowState.Next(lowCurve, 95, 3, 85) == 100, "The failsafe must override a curve that tops out low");
Check(lowState.Next(lowCurve, 50, 3, 0) is > 20 and < 40, "A zero failsafe disables it rather than pinning the fan at 100%");

var profile = new FanProfile();
Check(profile.Mode == FanMode.Auto, "A new fan profile must start on Auto");
Check(profile.HysteresisC > 0, "A new fan profile must carry a hysteresis");
Check(profile.StartTemperatureC < profile.MaxTemperatureC, "The default ramp window must be the right way round");
Check(profile.Window == (profile.StartTemperatureC, profile.MaxTemperatureC), "The window the service reads must be the stored temperatures");
var roundTripped = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(
    new AppSettings { FanProfiles = { new FanProfile { ControlId = "/nvidiagpu/0/control/0", Mode = FanMode.Sensor, StartTemperatureC = 55, MaxTemperatureC = 80 } } }))!;
Check(roundTripped.FanProfiles.Count == 1 && roundTripped.FanProfiles[0].ControlId == "/nvidiagpu/0/control/0"
      && roundTripped.FanProfiles[0].Mode == FanMode.Sensor
      && roundTripped.FanProfiles[0].StartTemperatureC == 55
      && roundTripped.FanProfiles[0].MaxTemperatureC == 80, "Fan profiles must round-trip through settings");
// A settings file written before this shape existed must land on Auto rather than resuming
// something the user can no longer see. This is a safety default, not a convenience one.
var legacy = JsonSerializer.Deserialize<AppSettings>(
    "{\"FanProfiles\":[{\"ControlId\":\"/nvidiagpu/0/control/0\",\"Enabled\":true,\"Points\":[{\"TemperatureC\":40,\"Percent\":90}]}]}")!;
Check(legacy.FanProfiles.Count == 1 && legacy.FanProfiles[0].Mode == FanMode.Auto,
    "A fan profile from an older settings shape must fall back to Auto, never to driving the fan");

// ---- which header is which ----
// A board says nothing about a header except its label, so the label is all there is to go on.
// The names below are the ones an ASUS ROG STRIX B850-I actually reports; this machine has no
// motherboard fans at all, so without this the rules would ship unchecked.
foreach (var (name, icon) in new[]
{
    ("CPU Fan", "cpufan"), ("Chassis Fan", "chassis"), ("Extra Flow Fan", "flow"),
    ("AIO Pump", "pump"), ("Chassis Fan #2", "chassis"), ("Case Fan", "chassis"),
    ("System Fan", "chassis"), ("Fan #4", "fan"),
    // Silkscreen spellings, which is what comes through on a chip with no friendly name table.
    ("CPU_FAN1", "cpufan"), ("CPU_OPT", "cpufan"), ("CHA_FAN2", "chassis"), ("SYS_FAN3", "chassis"),
    ("PUMP_FAN1", "pump"), ("W_PUMP+", "pump"), ("AIO_PUMP", "pump"), ("Water Pump", "pump"),
})
    Check(FanNaming.IconFor(name) == icon, $"'{name}' must be drawn as {icon}, not {FanNaming.IconFor(name)}");

// A pump is checked before "CPU" so a header labelled "CPU Pump" is a pump, not a fan.
Check(FanNaming.IconFor("CPU Pump") == "pump", "A CPU pump must be drawn as a pump");
// What it follows is a coarser question than what it is drawn as: a pump and a CPU fan are
// different glyphs but both ramp on the CPU package.
Check(FanNaming.IsCpuServing("CPU Fan") && FanNaming.IsCpuServing("AIO Pump"),
    "A CPU fan and an AIO pump must both follow the CPU");
Check(!FanNaming.IsCpuServing("Chassis Fan") && !FanNaming.IsCpuServing("Extra Flow Fan"),
    "A chassis fan must not be pinned to the CPU — it follows whichever of CPU and GPU is hotter");
// Whatever the label, the row still gets a fan of some kind: this page is a list of fans, and a
// header nobody anticipated should not be the one row that looks like a piece of hardware.
foreach (var odd in new[] { "Fan #7", "High Amp Fan", "VRM Fan", "M.2 Fan", "" })
    Check(new[] { "fan", "cpufan", "chassis", "flow", "pump" }.Contains(FanNaming.IconFor(odd)),
        $"'{odd}' must still resolve to a known glyph");

// ---- remembering where the window was ----
// The saved rectangle names a desktop that may have changed since: a monitor unplugged, a
// laptop undocked, displays rearranged. None of those layouts exist on this machine, so the
// rule is checked here rather than discovered by someone whose window opened out of sight.
var primary = new Box(0, 0, 1920, 1040);
var secondLeft = new Box(-1920, 0, 1920, 1040);
Check(WindowPlacement.IsReachable(new Box(100, 100, 1040, 760), new[] { primary }),
    "A window on the only screen must be restored");
Check(WindowPlacement.IsReachable(new Box(-1800, 200, 1040, 760), new[] { primary, secondLeft }),
    "A window on a second screen to the left must be restored while that screen is attached");
Check(!WindowPlacement.IsReachable(new Box(-1800, 200, 1040, 760), new[] { primary }),
    "A window on a screen that is gone must not be restored onto nothing");
// The window body overlapping is not enough: what has to be reachable is the title bar, or
// the window cannot be dragged back into view.
Check(!WindowPlacement.IsReachable(new Box(300, -400, 1040, 760), new[] { primary }),
    "A window whose title bar is above the screen must not be restored");
Check(!WindowPlacement.IsReachable(new Box(1900, 500, 1040, 760), new[] { primary }),
    "A window with only a sliver on screen must not be restored");
Check(WindowPlacement.IsReachable(new Box(1800, 500, 1040, 760), new[] { primary }),
    "A window still holding a grabbable strip must be restored");

var placed = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(
    new AppSettings { WindowLeft = 120, WindowTop = 64, WindowWidth = 900, WindowHeight = 600, WindowMaximized = true }))!;
Check(placed.WindowLeft == 120 && placed.WindowTop == 64 && placed.WindowWidth == 900
      && placed.WindowHeight == 600 && placed.WindowMaximized,
    "A window's size and position must survive a restart");
Check(oldSettings.WindowWidth is null && oldSettings.WindowLeft is null,
    "Settings from before this existed must have no placement, so the window opens centred as it always did");

// ---- where the app keeps its files ----
// A momo-data folder beside the executable makes it portable; without one it writes to
// %APPDATA%. The default has to stay %APPDATA% because the same executable is handed to people
// who drop it in Program Files or run it out of a synced folder.
const string appData = @"C:\Users\x\AppData\Roaming\StatusMonitor";
Check(AppSettings.ResolveDataDir(@"D:\tools\status", _ => true, appData)
      == Path.Combine(@"D:\tools\status", AppSettings.PortableFolderName),
    "A momo-data folder beside the executable must win");
Check(AppSettings.ResolveDataDir(@"D:\tools\status", _ => false, appData) == appData,
    "Without that folder the app must keep writing to %APPDATA%");
Check(AppSettings.ResolveDataDir(null, _ => true, appData) == appData,
    "An unknown executable location must fall back to %APPDATA% rather than guessing");
Check(AppSettings.ResolveDataDir("", _ => true, appData) == appData,
    "An empty executable location must fall back too");

// ---- estimating a graphics card that reports nothing ----
// Plenty of cards have no power telemetry at all: a Pascal Quadro answers nvidia-smi's
// power.draw with N/A, and the NVML energy counter that would replace it needs Volta or newer.
// Those used to contribute zero watts, understating the total by more than the total's own
// CPU figure on a machine like that.
Check(PowerModel.LookupGpuTdp("NVIDIA Quadro P1000") == 47, "The P1000's board power must be known");
Check(PowerModel.LookupGpuTdp("NVIDIA GeForce RTX 5090") == 575, "The 5090's board power must be known");
// Longest match wins, or every Ti is quietly rated as the card below it.
Check(PowerModel.LookupGpuTdp("NVIDIA GeForce RTX 4070 Ti") == 285, "A Ti must not match the plain card");
Check(PowerModel.LookupGpuTdp("NVIDIA GeForce RTX 2080 Ti") == 250, "A Ti must not match the plain card");
// An unknown card returns nothing rather than an invented rating: a guessed TDP would flow into
// the wattage, the energy total, the carbon figure and the cost, looking exactly as solid as a
// real reading all the way down.
Check(PowerModel.LookupGpuTdp("Some Unreleased Card 9999") == 0, "An unknown card must not be given a rating");

double idleGpu = PowerModel.EstimateGpuWatts(47, 0);
double fullGpu = PowerModel.EstimateGpuWatts(47, 100);
Check(Math.Abs(idleGpu - 5.82) < 0.01, $"A 47 W card must idle near 5.8 W, got {idleGpu:N2}");
Check(Math.Abs(fullGpu - 47) < 0.01, $"A card must not be estimated above its board power, got {fullGpu:N2}");
Check(PowerModel.EstimateGpuWatts(47, 50) > idleGpu && PowerModel.EstimateGpuWatts(47, 50) < fullGpu,
    "The curve must rise between idle and full");
// The two curves differ deliberately. A GPU idles far lower against its rating than a CPU, and
// a card is held to its board power while a CPU boosts past its TDP.
Check(PowerModel.EstimateGpuWatts(100, 0) < PowerModel.EstimateCpuWatts(100, 0),
    "A GPU must be estimated to idle lower than a CPU of the same rating");
Check(PowerModel.EstimateGpuWatts(100, 100) < PowerModel.EstimateCpuWatts(100, 100),
    "A CPU may be estimated past its rating; a card may not");

// ---- how a card idles ----
// Idle board power does not scale with the rating, which a flat tenth assumed it did. Across
// the range the ratings span more than tenfold while the idle figures span about fourfold, so
// the share model reads low on small cards and roughly double on the largest ones.
Check(Math.Abs(PowerModel.GpuIdleWatts(47) - 5.82) < 0.001, "A 47 W card idles on the slope, not on a share");
Check(Math.Abs(PowerModel.GpuIdleWatts(250) - 18.0) < 0.001, "A 250 W card must idle near 18 W");
Check(PowerModel.GpuIdleWatts(450) == 25.0, "The largest cards must hit the idle cap, not a share of the rating");
Check(PowerModel.GpuIdleWatts(450) < 450 * 0.10,
    "A 450 W card must no longer be said to idle at a tenth of its rating");
// The small end has two guards and the tighter one has to win, or a card is estimated to idle
// at more than it can draw.
Check(PowerModel.GpuIdleWatts(12) <= 12 * 0.25 + 0.001, "Idle must stay under a quarter of a small card's rating");
Check(PowerModel.GpuIdleWatts(12) < PowerModel.GpuIdleWatts(47), "Idle must still rise with the rating");
Check(PowerModel.GpuIdleWatts(575) == PowerModel.GpuIdleWatts(450), "Past the cap every card idles the same");
for (int tdp = 10; tdp <= 600; tdp += 10)
    Check(PowerModel.GpuIdleWatts(tdp) < tdp && PowerModel.GpuIdleWatts(tdp) > 0,
        $"Idle must sit between nothing and the rating at {tdp} W");

// ---- Intel parts and their suffixes ----
// 11th gen was absent from the table. The machine that exposed it has an i5-11400, whose
// 65 W the fallback happened to produce; the i5-11600K one shelf over is 125 W, and the
// fallback would have halved it. The CPU figure is usually the largest term in the total,
// so a wrong rating here is not a detail.
Check(PowerModel.LookupCpuTdp("11th Gen Intel(R) Core(TM) i5-11400 @ 2.60GHz") == 65, "The i5-11400 is a 65 W part");
Check(PowerModel.LookupCpuTdp("11th Gen Intel(R) Core(TM) i5-11600K") == 125, "The i5-11600K is a 125 W part");
Check(PowerModel.LookupCpuTdp("11th Gen Intel(R) Core(TM) i9-11900K") == 125, "The i9-11900K is a 125 W part");
Check(PowerModel.LookupCpuTdp("11th Gen Intel(R) Core(TM) i9-11900") == 65, "The i9-11900 without the K is 65 W");
Check(PowerModel.LookupCpuTdp("Intel(R) Core(TM) i5-11400T") == 35, "A T part is a 35 W part");
// Suffix rows must be tested before the bare model, since every suffixed name contains it.
// This used to be wrong in the other direction: a bare i9-14900 was rated as its own K.
Check(PowerModel.LookupCpuTdp("Intel(R) Core(TM) i9-14900K") == 125, "A K part must not be read as its base model");
Check(PowerModel.LookupCpuTdp("Intel(R) Core(TM) i9-14900") == 65, "A base model must not be read as its K part");
Check(PowerModel.LookupCpuTdp("Intel(R) Core(TM) i9-13900KS") == 125, "KS is covered by the K row");
Check(PowerModel.LookupCpuTdp("Intel(R) Core(TM) i7-12700KF") == 125, "KF is covered by the K row");
Check(PowerModel.LookupCpuTdp("Intel(R) Core(TM) i7-14700") == 65, "The i7-14700 without the K is 65 W");
Check(PowerModel.LookupCpuTdp("Intel(R) Core(TM) i5-13600K") == 125, "The i5-13600K is a 125 W part");
Check(PowerModel.LookupCpuTdp("Intel(R) Core(TM) i3-12100T") == 35, "A T i3 is a 35 W part");
// An unknown CPU still falls back rather than reading zero, because a CPU always draws power.
Check(PowerModel.LookupCpuTdp("Some Future Intel Thing") == 65, "An unknown CPU must fall back, not read zero");

// ---- fans ----
// Fans were counted as nothing while visibly turning. This is the one piece of board overhead
// with a measurement under it: the tachometers are already read for the Fans page.
Check(PowerModel.FanWatts(0) == 0, "A stopped fan costs nothing");
Check(Math.Abs(PowerModel.FanWatts(1900) - 1.7) < 0.001, "A 120 mm fan at full tilt is the anchor");
// The affinity laws, which are the whole reason a quiet fan is nearly free.
double slow = PowerModel.FanWatts(600), fast = PowerModel.FanWatts(1200);
Check(Math.Abs(fast / slow - 8.0) < 0.01, $"Fan power must go with the cube of speed, got {fast / slow:N2}x for double the speed");
Check(slow < 0.1, $"A fan loafing at 600 rpm must be nearly free, got {slow:N3} W");
// A tachometer does not say how big the fan is, and a small one spins fast while drawing
// little, so the cube law has to be stopped somewhere.
Check(PowerModel.FanWatts(5000) <= 4.0, "A small fast fan must not run away with the cube law");

// ---- the board ----
// The regulator loss is the CPU's alone. A card's rating is measured at its own connectors, so
// its regulators are already inside the figure used for it - the same double count the
// integrated GPU rule exists to prevent.
Check(Math.Abs(PowerModel.VrmLossWatts(50) - 5.0) < 0.001, "A tenth of what reaches the CPU is lost on the way");
Check(PowerModel.VrmLossWatts(0) == 0 && PowerModel.VrmLossWatts(-5) == 0, "Regulator loss must never be negative");

// ---- the power supply ----
// Nothing is applied without a rating: an unknown supply returns zero, which the caller must
// read as "leave the figure alone", not as "loses everything".
Check(PowerModel.PsuEfficiency("bronze", 30, 0) == 0, "An undescribed supply must not produce a loss");
Check(PowerModel.WallWatts(30, 0) == 30, "With no efficiency the figure passes through untouched");
Check(Math.Abs(PowerModel.WallWatts(40, 0.80) - 50) < 0.001, "The wall sees the draw divided by the efficiency");
// The rated load points, which are what a badge actually promises.
Check(Math.Abs(PowerModel.PsuEfficiency("bronze", 100, 500) - 0.82) < 0.001, "Bronze is 82% at a fifth load");
Check(Math.Abs(PowerModel.PsuEfficiency("gold", 250, 500) - 0.90) < 0.001, "Gold is 90% at half load");
Check(Math.Abs(PowerModel.PsuEfficiency("titanium", 50, 500) - 0.90) < 0.001, "Titanium is the one rated down at a tenth load");
// The point of the curve: the same unit is far worse where a desktop actually idles.
double midLoad = PowerModel.PsuEfficiency("bronze", 250, 500);
double idleLoad = PowerModel.PsuEfficiency("bronze", 25, 500);
Check(idleLoad < midLoad - 0.05,
    $"Idling at 5% load must cost far more than mid load, got {idleLoad:P0} against {midLoad:P0}");
Check(idleLoad < 0.75, $"A 500 W bronze unit at 25 W must be well under its badge, got {idleLoad:P0}");
// A better badge must be better everywhere, or the ordering means nothing.
foreach (double draw in new[] { 10.0, 25.0, 50.0, 100.0, 250.0, 500.0 })
    Check(PowerModel.PsuEfficiency("titanium", draw, 500) > PowerModel.PsuEfficiency("gold", draw, 500) &&
          PowerModel.PsuEfficiency("gold", draw, 500) > PowerModel.PsuEfficiency("bronze", draw, 500) &&
          PowerModel.PsuEfficiency("bronze", draw, 500) > PowerModel.PsuEfficiency("white", draw, 500),
        $"The badges must rank in order at {draw} W");
// An unknown class must not throw or zero the figure; it falls back to the commonest badge.
Check(Math.Abs(PowerModel.PsuEfficiency("chrome", 100, 500) - PowerModel.PsuEfficiency("bronze", 100, 500)) < 0.001,
    "An unrecognised badge must fall back rather than produce nothing");
// Every class, every load: an efficiency outside these bounds would be a bug, not a supply.
foreach (string cls in PowerModel.EfficiencyClasses)
    for (int draw = 5; draw <= 605; draw += 40)
    {
        double e = PowerModel.PsuEfficiency(cls, draw, 500);
        Check(e > 0.4 && e < 1.0, $"{cls} at {draw} W produced an impossible efficiency of {e:P0}");
        Check(PowerModel.WallWatts(draw, e) > draw, $"{cls} at {draw} W must cost more at the wall than at the parts");
    }

// ---- a card nobody recognises ----
// The table is always behind the market, and asking the driver does not rescue it: a card that
// cannot report its power cannot report its power limit either. So the owner can supply the
// number, and that answer has to outlast a restart and refuse nonsense.
var tdpSettings = new AppSettings();
int saves = 0;
var tdpRow = new GpuTdpVm("Some Unreleased Card 9999", tdpSettings, () => saves++);
Check(tdpRow.Watts == "", "A card with no figure yet must show an empty box, not a zero");

tdpRow.Watts = "150";
Check(tdpSettings.GpuTdpWatts["Some Unreleased Card 9999"] == 150 && saves == 1,
    "A board power the owner types must be stored and saved");
Check(tdpRow.Watts == "150", "The box must show back what was stored");

tdpRow.Watts = "0";
Check(tdpRow.Watts == "150", "Zero watts is a typo, not a card, and must not replace a good figure");
tdpRow.Watts = "5000";
Check(tdpRow.Watts == "150", "A figure above any board on sale must be refused");
tdpRow.Watts = "not a number";
Check(tdpRow.Watts == "150", "Text must be refused rather than silently clearing the figure");

// Blank is a real answer: it puts the card back outside the total rather than meaning zero.
tdpRow.Watts = "";
Check(tdpRow.Watts == "" && !tdpSettings.GpuTdpWatts.ContainsKey("Some Unreleased Card 9999"),
    "Clearing the box must remove the figure, not store a zero");

tdpRow.Watts = "47";
var tdpRestored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(tdpSettings))!;
Check(tdpRestored.GpuTdpWatts.TryGetValue("Some Unreleased Card 9999", out double keptTdp) && keptTdp == 47,
    "A board power the owner supplied must survive a restart");
Check(oldSettings.GpuTdpWatts.Count == 0,
    "Settings from before this existed must simply have no figures, not fail to load");

Console.WriteLine($"PASS: {checks} assertions (alerts, GPU identity, bounded history, settings compatibility, fan curve, header naming, window placement, data location, power curves)");

