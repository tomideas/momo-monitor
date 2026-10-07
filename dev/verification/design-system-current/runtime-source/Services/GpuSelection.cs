using StatusMonitor.Models;

namespace StatusMonitor.Services;

public static class GpuSelection
{
    // Be conservative: unrecognized models stay visible rather than hiding a discrete GPU.
    public static bool IsKnownIntegrated(string name)
    {
        string normalized = System.Text.RegularExpressions.Regex.Replace(name ?? "", @"\((?:R|TM)\)|[®™]", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
        if (normalized.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            // Arc A/B (including Pro) boards are discrete. Core Ultra's Arc Graphics and
            // 130V/140V/130T/140T names are integrated despite sharing the Arc brand.
            if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"\bArc\s+(?:Pro\s+)?[AB]\d{2,3}M?\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                normalized.Contains("MAX", StringComparison.OrdinalIgnoreCase)) return false;
            return normalized.Contains("UHD Graphics", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("HD Graphics", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("Iris", StringComparison.OrdinalIgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^Intel (?:Integrated )?Graphics$|\bArc (?:Graphics|\d{3}[VT] (?:GPU|Graphics))\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return normalized.Equals("AMD Radeon Graphics", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Radeon Graphics", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(normalized,
                @"\bRadeon\s+(?:(?:610|660|680|740|760|780|840|860|880|890)M|(?:8050|8060)S)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

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
