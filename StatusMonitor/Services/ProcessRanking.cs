using StatusMonitor.Models;

namespace StatusMonitor.Services;

/// <summary>Rank the complete sample before limiting rows; RAM inspection must not sort only the power top ten.</summary>
public static class ProcessRanking
{
    public static IEnumerable<ProcessRow> Order(IEnumerable<ProcessRow> rows, string metric) => metric switch
    {
        "ram" => rows.OrderByDescending(r => r.RamMb).ThenBy(r => r.Name),
        "cpu" => rows.OrderByDescending(r => r.CpuPercent).ThenBy(r => r.Name),
        "gpu" => rows.OrderByDescending(r => r.GpuPercent ?? 0).ThenBy(r => r.Name),
        "disk" => rows.OrderByDescending(r => r.DiskReadBytesPerSec + r.DiskWriteBytesPerSec).ThenBy(r => r.Name),
        _ => rows.OrderByDescending(r => r.PowerWatts).ThenByDescending(r => r.CpuPercent)
    };
}
