using System.Diagnostics;
using Microsoft.Win32;

namespace StatusMonitor.Sensors;

/// <summary>
/// Effective CPU clock via the "% Processor Performance" counter (the same
/// source Task Manager uses), so it works without a kernel driver.
/// current MHz = base MHz × performance / 100.
/// </summary>
public sealed class CpuClockSensor : IDisposable
{
    private PerformanceCounter? _performance;
    private readonly double _baseMhz;
    private bool _initialized;

    public CpuClockSensor()
    {
        _baseMhz = ReadBaseMhz();
    }

    public double? Read()
    {
        EnsureInitialized();
        if (_performance is null || _baseMhz <= 0) return null;
        try
        {
            double percent = _performance.NextValue();
            return percent > 0 ? _baseMhz * percent / 100.0 : null;
        }
        catch { return null; }
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            _performance = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true);
            _performance.NextValue();
        }
        catch { _performance = null; }
    }

    private static double ReadBaseMhz()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("~MHz") is int mhz && mhz > 0) return mhz;
        }
        catch { }
        return 0;
    }

    public void Dispose() => _performance?.Dispose();
}
