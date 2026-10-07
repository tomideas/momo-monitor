using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StatusMonitor.Services;

/// <summary>Monotonic Windows uptime excluding time spent suspended or hibernating.</summary>
public static class AwakeClock
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong interruptTime);

    public static double Seconds
    {
        get
        {
            if (OperatingSystem.IsWindows() && QueryUnbiasedInterruptTime(out ulong value))
                return value / 10_000_000.0;
            // The power event handler resets integration on resume if this fallback is used.
            return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        }
    }
}
