using System.Diagnostics;
using System.Runtime.InteropServices;
using StatusMonitor.Models;

namespace StatusMonitor.Sensors;

/// <summary>
/// Per-process CPU, RAM, GPU and disk I/O sampling.
/// CPU and I/O are computed from deltas between successive samples.
/// Per-process GPU utilisation comes from the "GPU Engine" performance counters.
/// </summary>
public sealed class ProcessSensor
{
    private sealed class Prev
    {
        public TimeSpan Cpu;
        public ulong Read;
        public ulong Write;
    }

    private readonly Dictionary<int, Prev> _prev = new();
    private readonly Dictionary<string, CounterSample> _gpuPrev = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _cores = Environment.ProcessorCount;

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS counters);

    public (List<ProcessRow> Rows, double SumCpu, double SumGpu) Sample(double elapsedSec)
    {
        if (elapsedSec <= 0) elapsedSec = 1.0;

        var gpuByPid = ReadGpuByPid();
        var byName = new Dictionary<string, ProcessRow>(StringComparer.OrdinalIgnoreCase);
        double sumCpu = 0, sumGpu = 0;

        foreach (var p in Process.GetProcesses())
        {
            int pid = p.Id;
            try
            {
                string name;
                try { name = p.ProcessName; }
                catch { continue; }

                double cpuPct = 0, ramMb = 0, readBps = 0, writeBps = 0;

                bool hasCpu = false;
                TimeSpan cpuTime = TimeSpan.Zero;
                try { cpuTime = p.TotalProcessorTime; hasCpu = true; } catch { }

                bool hasIo = false;
                ulong ioRead = 0, ioWrite = 0;
                try
                {
                    if (GetProcessIoCounters(p.Handle, out var io))
                    {
                        ioRead = io.ReadTransferCount;
                        ioWrite = io.WriteTransferCount;
                        hasIo = true;
                    }
                }
                catch { }

                try { ramMb = p.WorkingSet64 / 1048576.0; } catch { }

                _prev.TryGetValue(pid, out var prev);
                if (prev is not null)
                {
                    if (hasCpu)
                    {
                        double delta = (cpuTime - prev.Cpu).TotalSeconds;
                        cpuPct = Math.Clamp(delta / elapsedSec / _cores * 100.0, 0.0, 100.0 * _cores);
                    }
                    if (hasIo)
                    {
                        readBps = Math.Max(0, ((double)ioRead - prev.Read) / elapsedSec);
                        writeBps = Math.Max(0, ((double)ioWrite - prev.Write) / elapsedSec);
                    }
                }

                _prev[pid] = new Prev
                {
                    Cpu = hasCpu ? cpuTime : prev?.Cpu ?? TimeSpan.Zero,
                    Read = hasIo ? ioRead : prev?.Read ?? 0,
                    Write = hasIo ? ioWrite : prev?.Write ?? 0,
                };

                double gpuPct = gpuByPid.TryGetValue(pid, out double g) ? g : 0;
                sumCpu += cpuPct;
                sumGpu += gpuPct;

                if (!byName.TryGetValue(name, out var row))
                {
                    row = new ProcessRow { Name = name };
                    byName[name] = row;
                }
                row.CpuPercent += cpuPct;
                row.GpuPercent = (row.GpuPercent ?? 0) + (gpuByPid.ContainsKey(pid) ? gpuPct : 0);
                row.RamMb += ramMb;
                row.DiskReadBytesPerSec += readBps;
                row.DiskWriteBytesPerSec += writeBps;
            }
            catch { /* process vanished or access denied */ }
            finally { p.Dispose(); }
        }

        PruneDeadPids();
        return (byName.Values.ToList(), sumCpu, sumGpu);
    }

    private void PruneDeadPids()
    {
        if (_prev.Count <= 512) return;
        var alive = new HashSet<int>();
        foreach (var p in Process.GetProcesses())
        {
            alive.Add(p.Id);
            p.Dispose();
        }
        foreach (int key in _prev.Keys.ToList())
            if (!alive.Contains(key)) _prev.Remove(key);
    }

    /// <summary>
    /// Reads per-process GPU utilisation from the "GPU Engine" counters.
    /// <para>
    /// The whole category is read in one call and the percentages are derived from the raw
    /// samples. The obvious implementation — a cached <see cref="PerformanceCounter"/> per
    /// engine instance, each asked for <c>NextValue</c> — measured at 227 ms per tick on this
    /// machine, because there is one instance per process per engine type (427 of them here)
    /// and every call goes back to the performance-data provider. One
    /// <see cref="PerformanceCounterCategory.ReadCategory"/> returns the same data in 1.3 ms.
    /// </para>
    /// <para>
    /// <see cref="CounterSample.Calculate(CounterSample, CounterSample)"/> applies the counter
    /// type's own formula, so the numbers are the ones <c>NextValue</c> would have produced —
    /// this is the same reading taken a cheaper way, not an approximation.
    /// </para>
    /// </summary>
    private Dictionary<int, double> ReadGpuByPid()
    {
        var result = new Dictionary<int, double>();
        InstanceDataCollection? utilization;
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine").ReadCategory();
            utilization = category["Utilization Percentage"];
        }
        catch { return result; }
        if (utilization is null) return result;

        var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (InstanceData data in utilization.Values)
        {
            string name = data.InstanceName;
            if (!name.Contains("engtype", StringComparison.OrdinalIgnoreCase)) continue;
            live.Add(name);

            var sample = data.Sample;
            bool hasPrevious = _gpuPrev.TryGetValue(name, out var previous);
            _gpuPrev[name] = sample;
            // A rate needs two samples; the first sighting of an engine only seeds one. The
            // per-instance counter behaved the same way — its first NextValue returned 0.
            if (!hasPrevious) continue;

            double value;
            try { value = CounterSample.Calculate(previous, sample); }
            catch { continue; }
            if (value <= 0) continue;

            int pid = ParsePid(name);
            if (pid < 0) continue;
            result.TryGetValue(pid, out double current);
            result[pid] = current + value;
        }

        // Engine instances come and go with their processes, so the seed map is pruned to
        // what the provider still reports.
        if (_gpuPrev.Count > live.Count)
            foreach (string key in _gpuPrev.Keys.ToList())
                if (!live.Contains(key)) _gpuPrev.Remove(key);

        foreach (int key in result.Keys.ToList())
            if (result[key] > 100.0) result[key] = 100.0;
        return result;
    }

    private static int ParsePid(string instanceName)
    {
        const string marker = "pid_";
        int start = instanceName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return -1;
        start += marker.Length;
        int end = instanceName.IndexOf('_', start);
        if (end < 0) end = instanceName.Length;
        return int.TryParse(instanceName.AsSpan(start, end - start), out int pid) ? pid : -1;
    }
}
