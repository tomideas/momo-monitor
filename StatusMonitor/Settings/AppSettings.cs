using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StatusMonitor.Settings;

public sealed record CountryPreset(string Label, double PricePerKwh, string CurrencyCode, double CarbonGPerKwh);

public sealed record CurrencyInfo(string Code, string Symbol);

/// <summary>
/// Which temperature a fan follows, as a category rather than a sensor.
/// <para>
/// Offering the raw sensor list meant eleven entries — every CPU core, Core Max, Core Average,
/// CPU Package, GPU Core, GPU Hot Spot — to answer a question that only has two useful answers.
/// The category picks the right sensor itself: the package reading for the CPU, the hot spot
/// for a GPU, because those are the ones a fan should react to.
/// </para>
/// </summary>

public enum FanMode
{
    /// <summary>The card's own firmware decides. The default, and what every fan reverts to.</summary>
    Auto,
    /// <summary>One fixed speed, ignoring temperature.</summary>
    Constant,
    /// <summary>Ramp between two temperatures, across the fan's own speed range.</summary>
    Sensor,
}

/// <summary>
/// A saved fan setting for one controllable fan. Keyed by the control sensor's identifier so a
/// profile follows the same fan across restarts; a machine whose hardware changed simply has
/// an orphaned entry, which is ignored rather than applied to the wrong fan.
/// </summary>
public sealed class FanProfile
{
    public string ControlId { get; set; } = "";

    /// <summary>
    /// A detached copy for the custom panel to edit. The panel works on this and hands it over
    /// only when Apply is pressed, which is what keeps a half-typed number off the hardware.
    /// </summary>
    public FanProfile Copy() => new()
    {
        ControlId = ControlId, Mode = Mode, ConstantPercent = ConstantPercent,
        StartTemperatureC = StartTemperatureC, MaxTemperatureC = MaxTemperatureC, HysteresisC = HysteresisC,
    };

    /// <summary>Takes the panel's values, on Apply. ControlId is identity and never travels.</summary>
    public void CopyFrom(FanProfile other)
    {
        Mode = other.Mode;
        ConstantPercent = other.ConstantPercent;
        StartTemperatureC = other.StartTemperatureC;
        MaxTemperatureC = other.MaxTemperatureC;
        HysteresisC = other.HysteresisC;
    }

    /// <summary>
    /// Auto unless the user chose otherwise — including for a settings file written before
    /// this shape existed, whose old fields simply do not bind. An upgrade therefore hands
    /// every fan back to its firmware rather than resuming a curve nobody can see any more.
    /// </summary>
    public FanMode Mode { get; set; } = FanMode.Auto;

    /// <summary>Speed for <see cref="FanMode.Constant"/>, clamped to the fan's range on write.</summary>
    public double ConstantPercent { get; set; } = 50;

    /// <summary>Below this the fan stays at its minimum.</summary>
    public double StartTemperatureC { get; set; } = 50;

    /// <summary>At and above this the fan is at its maximum. This is also the failsafe.</summary>
    public double MaxTemperatureC { get; set; } = 85;

    /// <summary>The ramp window, as the service reads it.</summary>
    [JsonIgnore]
    public (double StartC, double MaxC) Window => (StartTemperatureC, MaxTemperatureC);

    /// <summary>
    /// Degrees the temperature must fall below the point that set the current speed before the
    /// fan may slow down. Not exposed in the UI: it exists to stop the fan hunting audibly
    /// when a workload walks the temperature back and forth, and there is no reading of the
    /// tab where choosing a different number is the user's problem to solve.
    /// </summary>
    public double HysteresisC { get; set; } = 3;
}

/// <summary>User settings, persisted to %APPDATA%\StatusMonitor\settings.json.</summary>
public sealed class AppSettings
{
    public string Language { get; set; } = "zh";
    public string Font { get; set; } = "";
    public bool ReduceMotion { get; set; }
    public string PreferredGpuId { get; set; } = "";
    public bool HideIntegratedGpu { get; set; }
    /// <summary>
    /// Off by default: a panel that plants itself over every other window is a decision the
    /// user should make, not one they have to discover and undo. Lives in Settings rather
    /// than on the panel, which has no room for a control set once and then forgotten.
    /// </summary>
    public bool MiniTopmost { get; set; }
    public bool StartInMiniMode { get; set; }

    /// <summary>
    /// When true, closing or minimising hides to the tray and the app keeps sampling. When
    /// false (the default) the close button ends the program and minimise behaves normally —
    /// a window that vanishes into the tray without being asked is a surprise, not a feature.
    /// </summary>
    public bool RunInTray { get; set; }

    /// <summary>
    /// Whether the close button asks what to do. On by default: a window that vanishes into the
    /// tray unannounced is the behaviour people file as "I cannot quit this app", and one that
    /// exits while still accumulating energy totals loses the day's figures. Asking once, with
    /// the option never to be asked again, settles both without guessing.
    /// </summary>
    public bool AskOnClose { get; set; } = true;

    /// <summary>
    /// Which stretch the accumulated energy / carbon / cost figures cover. Defaults to All so
    /// an existing install keeps showing the number it always showed.
    /// </summary>
    public Services.EnergyPeriod EnergyPeriod { get; set; } = Services.EnergyPeriod.All;
    public string BackgroundTheme { get; set; } = "sky";
    public double? MiniLeft { get; set; }
    public double? MiniTop { get; set; }

    /// <summary>
    /// Where the main window was and how big, so it comes back the way it was left. Nullable
    /// as a set: a file written before this existed has none of them, and the window opens
    /// centred at its default size exactly as before.
    /// </summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>
    /// Board power in watts for cards this build does not recognise, keyed by the name the
    /// driver reports. The table of models can never cover everything on machines this is
    /// handed to, and the driver is no help — a card that cannot report its power cannot
    /// report its power limit either. The one party who reliably knows the number is the
    /// person who owns the card, so they can supply it, and then it is exact rather than
    /// approximated.
    /// </summary>
    public Dictionary<string, double> GpuTdpWatts { get; set; } = new();
    /// <summary>
    /// The chipset, controllers, USB and regulator loss that no sensor reports. Counted on
    /// every machine because every machine has them, and settable because a board with three
    /// add-in cards is not a board with none. Seven watts is the middle of a desktop's range.
    /// </summary>
    public double BoardBaseWatts { get; set; } = 7.0;

    /// <summary>
    /// Off by default, and deliberately: turning it on changes what the headline figure claims,
    /// from what the parts draw to what the wall sees, and it cannot be answered at all without
    /// the two facts below. A default of "on" would mean guessing the supply of every machine
    /// this is handed to.
    /// </summary>
    public bool WallPowerMode { get; set; }
    public int PsuRatedWatts { get; set; } = 500;
    public string PsuEfficiencyClass { get; set; } = "bronze";

    public int TrendSeconds { get; set; } = 60;
    public string TrendMetric { get; set; } = "cpu";
    public bool AlertsEnabled { get; set; } = true;
    public double CpuTemperatureLimit { get; set; } = 90;
    public double GpuTemperatureLimit { get; set; } = 85;
    public double MemoryLimit { get; set; } = 90;
    public double VramLimit { get; set; } = 90;
    public double DiskFreeLimit { get; set; } = 10;
    public int AlertHoldSeconds { get; set; } = 15;
    public int AlertCooldownSeconds { get; set; } = 300;
    public double PricePerKwh { get; set; } = 0.17;
    public string CurrencyCode { get; set; } = "USD";
    public double CarbonGPerKwh { get; set; } = 399.0;
    public int ProcessCount { get; set; } = 10;

    /// <summary>
    /// Seconds between sensor readings. Two is the default because it is what the reading
    /// costs: one round of sensors measures at ~82 ms, and the GPU driver query is most of
    /// that, so halving the rate halves the app's own draw. HWiNFO ships the same default for
    /// the same reason; Task Manager calls one second "Normal" and offers four as "Low".
    /// <para>
    /// It does not cost accuracy worth worrying about. Measured against a one-second
    /// reference series, a two-second interval shifts the accumulated energy by under 0.5%
    /// and five seconds by under 1%, with no systematic direction — the error is random and
    /// averages out the longer the app runs. See <see cref="IdleRefreshSeconds"/>.
    /// </para>
    /// </summary>
    public int RefreshSeconds { get; set; } = 2;

    /// <summary>
    /// Per-fan curves, keyed by the control sensor's identifier. Empty by default and every
    /// entry starts disabled: driving a fan is the one thing this app does that can damage
    /// hardware, so it never happens without the user turning it on for that specific fan.
    /// </summary>
    public List<FanProfile> FanProfiles { get; set; } = new();

    /// <summary>
    /// Interval used while no window is on screen (hidden to the tray, or minimised). Nothing
    /// is being read at that moment, so the only job left is keeping the energy total honest,
    /// and the measurement above says five seconds does that to within about 1%.
    /// </summary>
    public int IdleRefreshSeconds { get; set; } = 5;

    /// <summary>Clamps to the intervals the settings dialog offers, so a hand-edited file cannot ask for 0.</summary>
    [JsonIgnore]
    public int EffectiveRefreshSeconds => Math.Clamp(RefreshSeconds, 1, 10);

    [JsonIgnore]
    public int EffectiveIdleRefreshSeconds => Math.Max(EffectiveRefreshSeconds, Math.Clamp(IdleRefreshSeconds, 1, 60));

    /// <summary>
    /// The country is derived from the stored price + carbon values, so there is a
    /// single selector that drives both. Editing either value flips it to "Custom".
    /// </summary>
    [JsonIgnore]
    public string Country
    {
        get
        {
            var match = CountryPresets.FirstOrDefault(p =>
                p.Label != "Custom" &&
                Math.Abs(p.PricePerKwh - PricePerKwh) < 0.001 &&
                Math.Abs(p.CarbonGPerKwh - CarbonGPerKwh) < 0.5);
            return match?.Label ?? "Custom";
        }
    }

    /// <summary>Applies a country preset's electricity price, currency and carbon intensity.</summary>
    public void ApplyCountry(string label)
    {
        var preset = CountryPresets.FirstOrDefault(p => p.Label == label);
        if (preset is null || preset.Label == "Custom") return;
        PricePerKwh = preset.PricePerKwh;
        CurrencyCode = preset.CurrencyCode;
        CarbonGPerKwh = preset.CarbonGPerKwh;
    }

    public string CurrencySymbol =>
        Currencies.FirstOrDefault(c => c.Code == CurrencyCode)?.Symbol ?? CurrencyCode;

    /// <summary>The folder beside the executable that turns the app portable when it exists.</summary>
    public const string PortableFolderName = "momo-data";

    private static string? _dataDir;

    /// <summary>
    /// Where settings, totals and the energy history live.
    /// <para>
    /// Portable by opt-in: create a <c>momo-data</c> folder next to the executable and everything
    /// the app writes goes in there, where it can be found, backed up and moved with the app.
    /// Without that folder nothing changes and it writes to %APPDATA%, which matters because the
    /// same executable is handed to people who drop it in Program Files or run it from a synced
    /// folder — neither is a place to scatter files into by default.
    /// </para>
    /// <para>Resolved once: the answer cannot change while the process runs, and re-deriving it
    /// on every save would put a directory probe in the sampling path.</para>
    /// </summary>
    public static string DataDir => _dataDir ??= ResolveDataDir(
        Path.GetDirectoryName(Environment.ProcessPath), Directory.Exists,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StatusMonitor"));

    /// <summary>The rule itself, free of the filesystem so it can be tested.</summary>
    internal static string ResolveDataDir(string? exeDirectory, Func<string, bool> directoryExists, string appDataDir)
    {
        if (string.IsNullOrEmpty(exeDirectory)) return appDataDir;
        string portable = Path.Combine(exeDirectory, PortableFolderName);
        return directoryExists(portable) ? portable : appDataDir;
    }

    /// <summary>
    /// Moves an existing %APPDATA% history into a newly created portable folder. Without this,
    /// creating the folder would read as "the app forgot everything": the running totals and the
    /// day-by-day energy history would still exist, just somewhere the app no longer looks.
    /// Only ever copies into an empty folder, so it cannot overwrite a portable set.
    /// </summary>
    public static void AdoptExistingData()
    {
        try
        {
            string target = DataDir;
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StatusMonitor");
            if (string.Equals(target, appData, StringComparison.OrdinalIgnoreCase)) return;
            if (!Directory.Exists(appData)) return;
            Directory.CreateDirectory(target);
            foreach (string name in new[] { "settings.json", "totals.json", "energy-history.json" })
            {
                string from = Path.Combine(appData, name), to = Path.Combine(target, name);
                if (File.Exists(from) && !File.Exists(to)) File.Copy(from, to);
            }
        }
        catch { /* the app still runs with defaults; losing history is not worth failing to start */ }
    }

    private static string FilePath => Path.Combine(DataDir, "settings.json");
    private static string TotalsPath => Path.Combine(DataDir, "totals.json");

    public static AppSettings Load()
    {
        // Before the first read, so a folder created between runs picks up what was already there.
        AdoptExistingData();
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* non-fatal */ }
    }

    public static double LoadTotalEnergyWh()
    {
        try
        {
            if (File.Exists(TotalsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(TotalsPath));
                if (doc.RootElement.TryGetProperty("totalEnergyWh", out var v))
                    return v.GetDouble();
            }
        }
        catch { }
        return 0.0;
    }

    public static void SaveTotalEnergyWh(double value)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(TotalsPath, JsonSerializer.Serialize(new { totalEnergyWh = value }));
        }
        catch { }
    }

    // ----- Country presets: electricity price + currency + carbon intensity -----
    // Values from WattSeal (GPLv3) references, 2026. Only countries that have both
    // a price and a carbon figure are listed, so a single selection fills every field.

    public static readonly CountryPreset[] CountryPresets =
    {
        new("France", 0.24, "EUR", 42.0),
        new("Germany", 0.35, "EUR", 332.0),
        new("UK", 0.30, "GBP", 217.0),
        new("USA (average)", 0.19, "USD", 384.0),
        new("China", 0.51, "CNY", 555.0),
        new("India", 7.33, "INR", 707.0),
        new("Sweden", 2.30, "SEK", 35.0),
        new("Poland", 0.88, "PLN", 592.0),
        new("World average", 0.17, "USD", 399.0),
        new("Custom", 0.0, "USD", 0.0),
    };

    public static readonly CurrencyInfo[] Currencies =
    {
        new("USD", "$"), new("EUR", "€"), new("GBP", "£"), new("CHF", "CHF"),
        new("CAD", "CA$"), new("AUD", "A$"), new("JPY", "¥"), new("CNY", "¥"),
        new("INR", "₹"), new("BRL", "R$"), new("RUB", "₽"), new("KRW", "₩"),
        new("MXN", "MX$"), new("SGD", "S$"), new("HKD", "HK$"), new("SEK", "kr"),
        new("NOK", "kr"), new("DKK", "kr"), new("PLN", "zł"), new("TRY", "₺"),
        new("ZAR", "R"), new("PHP", "₱"), new("IDR", "Rp"), new("THB", "฿"),
        new("MYR", "RM"), new("VND", "₫"), new("ILS", "₪"), new("AED", "AED"),
        new("SAR", "SAR"), new("NZD", "NZ$"), new("CZK", "Kč"), new("HUF", "Ft"),
        new("RON", "lei"), new("BGN", "лв"), new("ARS", "AR$"), new("CLP", "CLP$"),
        new("COP", "COL$"), new("EGP", "E£"), new("NGN", "₦"), new("PKR", "Rs"),
        new("TWD", "NT$"), new("BTC", "₿"),
    };
}
