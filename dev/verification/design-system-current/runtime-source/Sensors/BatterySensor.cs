using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using StatusMonitor.Models;

namespace StatusMonitor.Sensors;

/// <summary>
/// Queries the Windows battery class driver without installing a driver or issuing SET commands.
/// Rates are battery-side telemetry, not AC-adapter input power. Queries are cached for five seconds.
/// </summary>
public sealed class BatterySensor
{
    private readonly object _gate = new();
    private BatteryReading _cached = new();
    private long _readAt = long.MinValue;
    private string? _chassisPlatform;

    public string PlatformName
    {
        get
        {
            lock (_gate)
            {
                var battery = Read();
                _chassisPlatform ??= ReadChassisPlatform();
                return ClassifyPlatform(battery.IsPresent, _chassisPlatform);
            }
        }
    }
    public bool IsPortableComputer => PlatformName == "Portable";

    public BatteryReading Read()
    {
        lock (_gate)
        {
            long now = Environment.TickCount64;
            if (_readAt != long.MinValue && now - _readAt < 5_000) return _cached;
            _readAt = now;
            _cached = ReadNative();
            return _cached;
        }
    }

    /// <summary>Forget cached telemetry when the system resumes or its sensors are retried.</summary>
    public void Invalidate()
    {
        lock (_gate) _readAt = long.MinValue;
    }

    internal static string ClassifyPlatform(bool hasSystemBattery, string chassisPlatform) =>
        hasSystemBattery || chassisPlatform == "Portable" ? "Portable" :
        chassisPlatform == "Desktop" ? "Desktop" : "Unknown";

    // BATTERY_STATUS.Rate is signed mW; int.MinValue is BATTERY_UNKNOWN_RATE.
    // BATTERY_CAPACITY_RELATIVE means both capacity and rate are in arbitrary units.
    internal sealed record NativeReading(uint Capabilities, uint FullCapacity, uint Capacity,
        uint PowerState, int Rate, bool HasStatus = true);

    internal static BatteryReading Interpret(IReadOnlyList<NativeReading> devices, bool? onAc,
        double? systemPercent, bool systemReportsBattery, bool enumerationComplete, string? error)
    {
        const uint systemBattery = 0x80000000, shortTerm = 0x20000000, relative = 0x40000000;
        var batteries = devices.Where(b => (b.Capabilities & systemBattery) != 0 &&
                                          (b.Capabilities & shortTerm) == 0).ToList();
        // A successful enumeration showing only UPS devices does not make a desktop portable.
        bool present = batteries.Count > 0 || (devices.Count == 0 && systemReportsBattery);
        var result = new BatteryReading
        {
            IsPresent = present, IsOnAcPower = onAc,
            ChargePercent = ValidPercent(systemPercent),
            Status = present ? "Unknown" : "NoBattery", Error = error
        };
        if (!present) { result.ChargePercent = null; return result; }
        if (batteries.Count == 0 || !enumerationComplete)
        {
            result.Status = "Unavailable";
            result.Error ??= "Battery power rate is unavailable.";
            return result;
        }

        bool charging = batteries.Any(b => (b.PowerState & 4) != 0);
        bool discharging = batteries.Any(b => (b.PowerState & 2) != 0);
        result.Status = charging && discharging ? "Mixed" : charging ? "Charging" :
                        discharging ? "Discharging" : "Idle";
        if (onAc == null && batteries.All(b => b.HasStatus))
            result.IsOnAcPower = batteries.All(b => (b.PowerState & 1) != 0) ? true :
                                  discharging ? false : null;

        bool completeCapacity = batteries.All(b => b.HasStatus && b.FullCapacity > 0 &&
            b.FullCapacity != uint.MaxValue && b.Capacity != uint.MaxValue);
        // Relative capacities from separate batteries cannot be added or capacity weighted.
        if (completeCapacity && (batteries.Count == 1 || batteries.All(b => (b.Capabilities & relative) == 0)))
            result.ChargePercent = Math.Clamp(batteries.Sum(b => (double)b.Capacity) * 100 /
                                             batteries.Sum(b => (double)b.FullCapacity), 0, 100);

        if (batteries.Any(b => !b.HasStatus || b.Rate == int.MinValue || (b.Capabilities & relative) != 0))
        {
            result.Error ??= batteries.Any(b => (b.Capabilities & relative) != 0)
                ? "Battery reports relative rate units; watts are unavailable."
                : "One or more battery power rates are unavailable.";
            return result;
        }
        bool positive = batteries.Any(b => b.Rate > 0), negative = batteries.Any(b => b.Rate < 0);
        // Two packs charging and discharging at once are not a whole-machine power source.
        if (positive && negative || charging && discharging)
        {
            result.Status = "Mixed";
            result.Error ??= "Battery packs have different charge/discharge directions.";
            return result;
        }
        if (positive)
        {
            if (discharging) { result.Error ??= "Battery rate and discharge state disagree."; return result; }
            result.ChargeWatts = batteries.Sum(b => (double)b.Rate) / 1_000;
            result.Status = "Charging";
        }
        else if (negative)
        {
            if (charging || onAc == true)
            { result.Error ??= "Battery rate and power-source state disagree."; return result; }
            result.DischargeWatts = -batteries.Sum(b => (double)b.Rate) / 1_000;
            result.Status = "Discharging";
            result.IsOnAcPower ??= false;
        }
        else if (discharging && onAc != true)
            result.DischargeWatts = 0;
        else if (charging)
            result.ChargeWatts = 0;
        return result;
    }

    private static double? ValidPercent(double? value) =>
        value is double v && double.IsFinite(v) && v is >= 0 and <= 100 ? v : null;

    private static BatteryReading ReadNative()
    {
        if (!OperatingSystem.IsWindows())
            return new BatteryReading { Status = "Unavailable", Error = "Windows battery API is unavailable." };
        bool? onAc = null;
        double? percent = null;
        bool systemReportsBattery = false;
        try
        {
            if (GetSystemPowerStatus(out var system))
            {
                onAc = system.ACLineStatus == 1 ? true : system.ACLineStatus == 0 ? false : null;
                systemReportsBattery = system.BatteryFlag != 255 && (system.BatteryFlag & 128) == 0;
                percent = systemReportsBattery && system.BatteryLifePercent <= 100 ? system.BatteryLifePercent : null;
            }
            var devices = QueryDevices(out bool complete, out string? error);
            return Interpret(devices, onAc, percent, systemReportsBattery, complete, error);
        }
        catch (Exception ex) when (ex is Win32Exception or DllNotFoundException or EntryPointNotFoundException
                                   or UnauthorizedAccessException or InvalidOperationException)
        {
            return Interpret(Array.Empty<NativeReading>(), onAc, percent, systemReportsBattery, false, ex.Message);
        }
    }

    private static List<NativeReading> QueryDevices(out bool complete, out string? error)
    {
        complete = true;
        error = null;
        var readings = new List<NativeReading>();
        // GUID_DEVICE_BATTERY, as declared in the Windows SDK poclass.h.
        Guid guid = new("72631e54-78a4-11d0-bcf7-00aa00b7b32a");
        IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x2 | 0x10);
        if (set == new IntPtr(-1))
        { complete = false; error = "Battery device enumeration is unavailable."; return readings; }
        try
        {
            // A bound prevents broken providers from making sampling unbounded.
            for (uint i = 0; i < 32; i++)
            {
                var device = new DeviceInterfaceData { Size = (uint)Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref device))
                {
                    if (Marshal.GetLastWin32Error() != 259) { complete = false; error ??= "Battery enumeration failed."; }
                    return readings;
                }
                SetupDiGetDeviceInterfaceDetail(set, ref device, IntPtr.Zero, 0, out uint needed, IntPtr.Zero);
                if (needed < 6 || needed > 65_536)
                { complete = false; error ??= "Battery device path is unavailable."; continue; }
                IntPtr detail = Marshal.AllocHGlobal((int)needed);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref device, detail, needed, out _, IntPtr.Zero))
                    { complete = false; error ??= "Battery device path is unavailable."; continue; }
                    string? path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    if (string.IsNullOrEmpty(path)) { complete = false; continue; }
                    // QUERY IOCTLs require FILE_READ_ACCESS only. No write handle or SET command.
                    using var handle = CreateFile(path, 0x80000000, 0x1 | 0x2, IntPtr.Zero, 3, 0, IntPtr.Zero);
                    uint wait = 0;
                    if (handle.IsInvalid || !QueryTag(handle, QueryTagCode, ref wait, 4, out uint tag, 4, out _, IntPtr.Zero) || tag == 0)
                    { complete = false; error ??= "Battery information could not be read."; continue; }
                    var query = new BatteryQueryInformation { Tag = tag };
                    if (!QueryInformation(handle, QueryInformationCode, ref query, 12, out var info,
                            (uint)Marshal.SizeOf<BatteryInformation>(), out _, IntPtr.Zero))
                    { complete = false; error ??= "Battery units could not be read."; continue; }
                    if ((info.Capabilities & 0x80000000) == 0 || (info.Capabilities & 0x20000000) != 0)
                    { readings.Add(new(info.Capabilities, info.FullCapacity, 0, 0, int.MinValue, false)); continue; }
                    var request = new BatteryWaitStatus { Tag = tag };
                    bool statusOk = QueryStatus(handle, QueryStatusCode, ref request, 20, out var status, 16, out _, IntPtr.Zero);
                    readings.Add(new(info.Capabilities, info.FullCapacity, statusOk ? status.Capacity : uint.MaxValue,
                        status.PowerState, statusOk ? status.Rate : int.MinValue, statusOk));
                    if (!statusOk) error ??= "Battery status could not be read.";
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
            complete = false;
            error ??= "Battery device enumeration exceeded its limit.";
            return readings;
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    private static string ReadChassisPlatform()
    {
        if (!OperatingSystem.IsWindows()) return "Unknown";
        try
        {
            using var search = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure");
            search.Options.Timeout = TimeSpan.FromSeconds(2);
            using var results = search.Get();
            var types = new List<ushort>();
            foreach (ManagementObject enclosure in results)
                using (enclosure)
                    if (enclosure["ChassisTypes"] is ushort[] values) types.AddRange(values);
            return ClassifyChassis(types);
        }
        catch { return "Unknown"; }
    }

    internal static string ClassifyChassis(IEnumerable<ushort> chassisTypes)
    {
        var types = chassisTypes.ToArray();
        if (types.Any(t => t is 8 or 9 or 10 or 11 or 14 or 30 or 31 or 32)) return "Portable";
        if (types.Any(t => t is 3 or 4 or 5 or 6 or 7 or 15 or 16 or 23 or 24 or 35 or 36)) return "Desktop";
        return "Unknown";
    }

    private const uint QueryTagCode = (0x29u << 16) | (1u << 14) | (0x10u << 2);
    private const uint QueryInformationCode = (0x29u << 16) | (1u << 14) | (0x11u << 2);
    private const uint QueryStatusCode = (0x29u << 16) | (1u << 14) | (0x13u << 2);

    [StructLayout(LayoutKind.Sequential)] private struct SystemPowerStatus
    { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceInterfaceData
    { public uint Size; public Guid InterfaceClassGuid; public uint Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct BatteryQueryInformation
    { public uint Tag, InformationLevel, AtRate; }
    [StructLayout(LayoutKind.Sequential)] private struct BatteryInformation
    {
        public uint Capabilities;
        public byte Technology, Reserved0, Reserved1, Reserved2, Chemistry0, Chemistry1, Chemistry2, Chemistry3;
        public uint DesignedCapacity, FullCapacity, DefaultAlert1, DefaultAlert2, CriticalBias, CycleCount;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BatteryWaitStatus
    { public uint Tag, Timeout, PowerState, LowCapacity, HighCapacity; }
    [StructLayout(LayoutKind.Sequential)] private struct BatteryStatus
    { public uint PowerState, Capacity, Voltage; public int Rate; }

    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref DeviceInterfaceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DeviceInterfaceData device, IntPtr detail, uint size, out uint required, IntPtr deviceInfo);
    [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryTag(SafeFileHandle handle, uint code, ref uint input, uint inputSize, out uint tag, uint outputSize, out uint bytes, IntPtr overlapped);
    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformation(SafeFileHandle handle, uint code, ref BatteryQueryInformation input, uint inputSize, out BatteryInformation information, uint outputSize, out uint bytes, IntPtr overlapped);
    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryStatus(SafeFileHandle handle, uint code, ref BatteryWaitStatus input, uint inputSize, out BatteryStatus status, uint outputSize, out uint bytes, IntPtr overlapped);
}
