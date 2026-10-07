using System.Net.NetworkInformation;
using StatusMonitor.Models;

namespace StatusMonitor.Sensors;

/// <summary>Whole-system network throughput via NetworkInterface statistics.</summary>
public sealed class NetworkSensor
{
    private long _prevRx;
    private long _prevTx;
    private bool _primed;

    public void Read(Snapshot snap, double elapsedSec)
    {
        long rx = 0, tx = 0;
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                try
                {
                    var stats = ni.GetIPv4Statistics();
                    rx += stats.BytesReceived;
                    tx += stats.BytesSent;
                }
                catch { /* adapter may not expose IP stats */ }
            }
        }
        catch { /* ignore */ }

        if (_primed && elapsedSec > 0)
        {
            snap.NetDownBytesPerSec = Math.Max(0, rx - _prevRx) / elapsedSec;
            snap.NetUpBytesPerSec = Math.Max(0, tx - _prevTx) / elapsedSec;
        }

        _prevRx = rx;
        _prevTx = tx;
        _primed = true;
    }
}
