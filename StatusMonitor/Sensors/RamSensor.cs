using System.Runtime.InteropServices;
using StatusMonitor.Models;

namespace StatusMonitor.Sensors;

/// <summary>System RAM usage via GlobalMemoryStatusEx (no driver required).</summary>
public static class RamSensor
{
    [StructLayout(LayoutKind.Sequential)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public static void Read(Snapshot snap)
    {
        try
        {
            var m = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(m))
            {
                snap.RamLoadPercent = m.dwMemoryLoad;
                snap.RamTotalGb = m.ullTotalPhys / 1073741824.0;
                snap.RamUsedGb = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
            }
        }
        catch { /* leave null */ }
    }
}
