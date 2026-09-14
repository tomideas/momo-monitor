using System.IO;
using System.Management;
using System.Text;
using StatusMonitor.I18n;
using StatusMonitor.Models;
using System.Globalization;

namespace StatusMonitor.Services;

/// <summary>Collects static system / hardware information (WMI) for the Info page.</summary>
public static class SystemInfoService
{
    public static List<InfoCard> Build(Snapshot snap)
    {
        var cards = new List<InfoCard>();
        Try(() => cards.AddRange(BuildCpu(snap)));
        Try(() => cards.AddRange(BuildGpu(snap)));
        Try(() => cards.Add(BuildMemory()));
        Try(() => cards.Add(BuildSystem()));
        Try(() => cards.Add(BuildStorage(snap)));
        Try(() => { var battery = BuildBattery(); if (battery.Items[0].Value != Loc.Instance["na"]) cards.Add(battery); });
        Try(() => cards.Add(BuildDisplay()));
        return cards;
    }

    private static void Try(Action action)
    {
        try { action(); } catch { /* a missing WMI class must not break the page */ }
    }

    private static (string name, int cores, int threads, double baseMhz)? _cpuStatic;

    /// <summary>Static CPU facts (name / cores / threads / base clock), WMI queried once and cached.</summary>
    public static (string name, int cores, int threads, double baseMhz) GetCpuStatic()
    {
        if (_cpuStatic is not null) return _cpuStatic.Value;

        string name = ""; int cores = 0; int threads = 0; double baseMhz = 0;
        foreach (ManagementObject o in new ManagementObjectSearcher(
                     "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor").Get())
        {
            name = (o["Name"]?.ToString() ?? "").Trim();
            cores = Convert.ToInt32(o["NumberOfCores"] ?? 0);
            threads = Convert.ToInt32(o["NumberOfLogicalProcessors"] ?? 0);
            baseMhz = Convert.ToDouble(o["MaxClockSpeed"] ?? 0);
            break;
        }

        _cpuStatic = (name, cores, threads, baseMhz);
        return _cpuStatic.Value;
    }

    private static List<InfoCard> BuildCpu(Snapshot snap)
    {
        var loc = Loc.Instance;
        var cards = new List<InfoCard>();
        foreach (ManagementObject o in new ManagementObjectSearcher(
                     "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L3CacheSize FROM Win32_Processor").Get())
        {
            string name = (o["Name"]?.ToString() ?? "").Trim();
            int cores = Convert.ToInt32(o["NumberOfCores"] ?? 0);
            int threads = Convert.ToInt32(o["NumberOfLogicalProcessors"] ?? 0);
            double maxMhz = Convert.ToDouble(o["MaxClockSpeed"] ?? 0);
            cards.Add(new InfoCard
            {
                Title = loc["info_cpu"],
                Subtitle = loc["info_cpu"],
                Items =
                {
                    new InfoItem { Label = loc["model"], Value = name },
                    new InfoItem { Label = loc["base_speed"], Value = maxMhz >= 1000 ? $"{maxMhz / 1000.0:0.00} GHz" : $"{maxMhz:0} MHz" },
                    new InfoItem { Label = loc["cores"], Value = string.Format(loc["core_threads"], cores, threads) },
                    new InfoItem { Label = loc["logical_processors"], Value = threads.ToString() },
                    new InfoItem { Label = loc["l3_cache"], Value = FormatL3Cache(o["L3CacheSize"]) },
                },
            });
        }
        return cards;
    }

    private static List<InfoCard> BuildGpu(Snapshot snap)
    {
        var loc = Loc.Instance;
        var cards = new List<InfoCard>();
        int index = 0;
        foreach (ManagementObject o in new ManagementObjectSearcher(
                      "SELECT Name, AdapterRAM, DriverVersion, DriverDate FROM Win32_VideoController").Get())
        {
            string name = (o["Name"]?.ToString() ?? "").Trim();
            if (name.Length == 0) continue;
            index++;
            string driverVer = (o["DriverVersion"]?.ToString() ?? "").Trim();
            var gpu = MatchGpu(snap, name);
            cards.Add(new InfoCard
            {
                Title = loc["info_gpu"],
                Subtitle = $"{loc["info_gpu"]} {index}",
                Items =
                {
                    new InfoItem { Label = loc["model"], Value = name },
                    GpuMemoryItem(gpu, o["AdapterRAM"]),
                    new InfoItem { Label = loc["driver_version"], Value = driverVer.Length > 0 ? driverVer : "—" },
                    new InfoItem { Label = loc["driver_date"], Value = FormatCimDateTime(o["DriverDate"]) },
                    // Beyond the 3-item summary, so it shows in the details panel only.
                    new InfoItem
                    {
                        Label = loc["memory_type"],
                        Value = gpu?.UsesSharedMemory == true ? loc["memory_shared"] : loc["memory_dedicated"],
                    },
                },
            });
        }
        return cards;
    }

    /// <summary>
    /// Graphics memory as used / total with a bar, labelled "VRAM" for every GPU so the column
    /// scans as one thing. Dedicated vs shared is reported separately, in the details panel.
    /// Falls back to Win32_VideoController.AdapterRAM only when no sensor reading exists;
    /// that field is a uint32 and silently caps at 4 GB, so it is marked as approximate.
    /// </summary>
    private static InfoItem GpuMemoryItem(ComponentReading? gpu, object? adapterRam)
    {
        var loc = Loc.Instance;
        const string label = "VRAM";

        double? totalMb = gpu?.MemoryTotalMb;
        double? usedMb = gpu?.MemoryUsedMb;
        if (totalMb is double total && total > 0)
        {
            if (usedMb is double used && used >= 0)
            {
                return new InfoItem
                {
                    Label = label,
                    Value = $"{loc["used"]} {Size(used / 1024)} / {Size(total / 1024)}  ({used / total * 100:0}%)",
                    Fraction = Math.Clamp(used / total, 0, 1),
                };
            }
            return new InfoItem { Label = label, Value = Size(total / 1024) };
        }

        string approx = FormatAdapterRam(adapterRam);
        return new InfoItem
        {
            Label = label,
            Value = approx == "—" ? approx : $"{approx} ({loc["approx"]})",
        };
    }

    /// <summary>Matches a WMI video controller name to its live LHM reading (case-insensitive).</summary>
    private static ComponentReading? MatchGpu(Snapshot snap, string name)
    {
        for (int i = 0; i < snap.GpuNames.Count; i++)
            if (snap.GpuNames[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return snap.Gpus[i];
        return null;
    }

    private static InfoCard BuildMemory()
    {
        var loc = Loc.Instance;
        double totalGb = 0, swapGb = 0;

        foreach (ManagementObject o in new ManagementObjectSearcher(
                     "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem").Get())
            totalGb = Convert.ToDouble(o["TotalPhysicalMemory"] ?? 0L) / 1073741824.0;

        foreach (ManagementObject o in new ManagementObjectSearcher(
                     "SELECT TotalVisibleMemorySize, TotalVirtualMemorySize FROM Win32_OperatingSystem").Get())
        {
            double visibleKb = Convert.ToDouble(o["TotalVisibleMemorySize"] ?? 0L);
            double virtualKb = Convert.ToDouble(o["TotalVirtualMemorySize"] ?? 0L);
            swapGb = Math.Max(0, virtualKb - visibleKb) / 1048576.0;
        }

        return new InfoCard
        {
            Title = loc["info_ram"],
            Subtitle = loc["info_ram"],
            Items =
            {
                new InfoItem { Label = loc["total_memory"], Value = $"{totalGb:0.00} GB" },
                new InfoItem { Label = loc["swap"], Value = $"{swapGb:0.00} GB" },
            },
        };
    }

    private static InfoCard BuildSystem()
    {
        var loc = Loc.Instance;
        string caption = "", build = "";
        foreach (ManagementObject o in new ManagementObjectSearcher(
                     "SELECT Caption, BuildNumber FROM Win32_OperatingSystem").Get())
        {
            caption = (o["Caption"]?.ToString() ?? "").Replace("Microsoft ", "").Trim();
            build = o["BuildNumber"]?.ToString() ?? "";
        }

        string os = build.Length > 0 ? $"{caption} ({build})" : caption;
        var card = new InfoCard { Title = loc["info_system"], Subtitle = loc["info_system"] };
        card.Items.Add(new InfoItem { Label = loc["os"], Value = os.Length > 0 ? os : "—" });
        card.Items.Add(new InfoItem { Label = loc["hostname"], Value = Environment.MachineName });
        return card;
    }

    /// <summary>
    /// Every fixed volume in one card, a bar per drive. Splitting them into a card each made a
    /// two-drive machine look like two unrelated components, and the old value was free/total
    /// under a label that read as used/total.
    /// </summary>
    private static InfoCard BuildStorage(Snapshot snap)
    {
        var loc = Loc.Instance;
        var card = new InfoCard { Title = loc["info_storage"], Subtitle = loc["info_storage"] };
        foreach (var disk in snap.Disks)
        {
            string name = disk.Label.Length > 0 ? $"{Trim(disk.Name)}  {disk.Label}" : Trim(disk.Name);
            card.Items.Add(new InfoItem
            {
                Label = name,
                Value = $"{loc["used"]} {Size(disk.UsedGb)} / {Size(disk.TotalGb)}  ({disk.UsedFraction * 100:0}%)",
                Fraction = disk.UsedFraction,
            });
        }
        if (card.Items.Count == 0) card.Items.Add(new InfoItem { Label = loc["space"], Value = loc["no_data"] });
        return card;
    }

    /// <summary>"C:\" reads better as "C:" in a label.</summary>
    private static string Trim(string root) => root.TrimEnd('\\', '/');

    /// <summary>TB past 1000 GB, so a 1.8 TB drive does not read as 1843.2 GB.</summary>
    private static string Size(double gb) =>
        gb >= 1000 ? (gb / 1024).ToString("0.00", CultureInfo.InvariantCulture) + " TB"
                   : gb.ToString("0.0", CultureInfo.InvariantCulture) + " GB";

    private static InfoCard BuildBattery()
    {
        var loc = Loc.Instance;
        string name = loc["na"], capacity = loc["na"];
        foreach (ManagementObject o in new ManagementObjectSearcher(
                     "SELECT Name, EstimatedChargeRemaining FROM Win32_Battery").Get())
        {
            name = o["Name"]?.ToString() ?? loc["na"];
            if (o["EstimatedChargeRemaining"] is not null)
                capacity = $"{Convert.ToInt32(o["EstimatedChargeRemaining"])}%";
            break;
        }
        return new InfoCard
        {
            Title = loc["info_battery"],
            Subtitle = loc["info_battery"],
            Items =
            {
                new InfoItem { Label = loc["name"], Value = name },
                new InfoItem { Label = loc["capacity"], Value = capacity },
            },
        };
    }

    private static InfoCard BuildDisplay()
    {
        var loc = Loc.Instance;
        string model = "", mode = "";

        try
        {
            foreach (ManagementObject o in new ManagementObjectSearcher(
                         "root\\wmi", "SELECT UserFriendlyName, ProductCodeID FROM WmiMonitorID").Get())
            {
                string friendly = Decode(o["UserFriendlyName"]);
                string code = Decode(o["ProductCodeID"]);
                model = friendly.Length > 0 ? friendly : code;
                if (model.Length > 0) break;
            }
        }
        catch { }

        foreach (ManagementObject o in new ManagementObjectSearcher(
                     "SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController").Get())
        {
            int w = Convert.ToInt32(o["CurrentHorizontalResolution"] ?? 0);
            int h = Convert.ToInt32(o["CurrentVerticalResolution"] ?? 0);
            int hz = Convert.ToInt32(o["CurrentRefreshRate"] ?? 0);
            if (w > 0 && h > 0) { mode = $"{w}x{h} @ {hz} Hz"; break; }
        }

        return new InfoCard
        {
            Title = loc["info_display"],
            Subtitle = loc["info_display"],
            Items =
            {
                new InfoItem { Label = loc["model"], Value = model.Length > 0 ? model : "—" },
                new InfoItem { Label = loc["mode"], Value = mode.Length > 0 ? mode : "—" },
            },
        };
    }

    private static string Decode(object? value)
    {
        if (value is not Array array) return "";
        var sb = new StringBuilder();
        foreach (object? item in array)
        {
            int code = Convert.ToInt32(item);
            if (code == 0) break;
            sb.Append((char)code);
        }
        return sb.ToString().Trim();
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;


    private static string FormatCimDateTime(object? value)
    {
        if (value is not string s || s.Length < 8) return "—";
        if (!DateTime.TryParseExact(s.AsSpan(0, 8), "yyyyMMdd", Inv,
                DateTimeStyles.None, out var dt))
            return "—";
        return dt.ToString("yyyy-MM-dd");
    }

    private static string FormatAdapterRam(object? value)
    {
        if (value is null) return "—";
        long bytes = Convert.ToInt64(value);
        if (bytes <= 0) return "—";
        return $"{bytes / 1073741824.0:0.0} GB";
    }

    private static string FormatL3Cache(object? value)
    {
        if (value is null) return "—";
        long kb = Convert.ToInt64(value);
        if (kb <= 0) return "—";
        if (kb >= 1048576) return $"{kb / 1048576.0:0.0} GB";
        return $"{kb / 1024.0:0.0} MB";
    }
}
