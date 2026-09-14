using StatusMonitor.Models;

namespace StatusMonitor.Services;

public static class GpuSelection
{
    // Be conservative: unrecognized models stay visible rather than hiding a discrete GPU.
    public static bool IsKnownIntegrated(string name) =>
        (name.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("UHD Graphics", StringComparison.OrdinalIgnoreCase) || name.Contains("HD Graphics", StringComparison.OrdinalIgnoreCase) ||
             (name.Contains("Iris", StringComparison.OrdinalIgnoreCase) && !name.Contains("MAX", StringComparison.OrdinalIgnoreCase)))) ||
        name.Contains("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("AMD Radeon Graphics", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(name, @"Radeon\s+(610|660|680|740|760|780|880|890)M\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static string Key(Snapshot snapshot, int index) => string.IsNullOrEmpty(snapshot.Gpus[index].Id)
        ? "gpu:" + (index < snapshot.GpuNames.Count ? snapshot.GpuNames[index] : index.ToString())
        : snapshot.Gpus[index].Id;

    public static List<int> Visible(Snapshot snapshot, string preferredId, bool hideIntegrated)
    {
        bool hasDiscrete = snapshot.Gpus.Any(g => !g.IsIntegrated);
        return Enumerable.Range(0, snapshot.Gpus.Count)
            .Where(i => !hideIntegrated || !hasDiscrete || !snapshot.Gpus[i].IsIntegrated)
            .OrderByDescending(i => Key(snapshot, i) == preferredId)
            .ThenBy(i => snapshot.Gpus[i].IsIntegrated)
            .ThenByDescending(i => snapshot.Gpus[i].MemoryTotalMb ?? 0)
            .ToList();
    }
}
