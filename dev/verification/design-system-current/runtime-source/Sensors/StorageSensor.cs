using System.Diagnostics;
using StatusMonitor.Models;

namespace StatusMonitor.Sensors;

/// <summary>Disk activity via PhysicalDisk performance counters.</summary>
public sealed class StorageSensor : IDisposable
{
    private PerformanceCounter? _diskTime;
    private PerformanceCounter? _read;
    private PerformanceCounter? _write;
    private bool _initialized;

    public void Read(Snapshot snap)
    {
        EnsureInitialized();
        try
        {
            if (_diskTime is not null)
                snap.StorageLoadPercent = Math.Clamp(_diskTime.NextValue(), 0.0, 100.0);
            if (_read is not null)
                snap.DiskReadBytesPerSec = Math.Max(0, _read.NextValue());
            if (_write is not null)
                snap.DiskWriteBytesPerSec = Math.Max(0, _write.NextValue());
        }
        catch { /* transient counter errors are ignored */ }
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        try { _diskTime = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true); _diskTime.NextValue(); }
        catch { _diskTime = null; }
        try { _read = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true); _read.NextValue(); }
        catch { _read = null; }
        try { _write = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true); _write.NextValue(); }
        catch { _write = null; }
    }

    public void Dispose()
    {
        _diskTime?.Dispose();
        _read?.Dispose();
        _write?.Dispose();
    }
}
