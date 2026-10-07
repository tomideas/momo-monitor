using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;
using StatusMonitor.Models;

namespace StatusMonitor.Services;

/// <summary>A user-requested CSV of the current sample. Never uploads or enumerates identity data.</summary>
public static class SensorReportService
{
    public static void Export(string csvPath, IEnumerable<SensorReading> readings, string platform,
        BatteryReading battery, DateTimeOffset? sampledAt = null, string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(battery);
        // Snapshot values are copied before touching the file, so export cannot observe a changing list.
        var rows = readings.Take(4_096).Select(r => new[]
        {
            Text(r.DeviceId), Text(r.DeviceName), Text(r.Id), Text(r.Name), Text(r.Type), Text(r.Unit),
            Text(r.Scope), Number(r.Value), Number(r.Minimum), Number(r.Maximum), Text(r.State)
        }).ToArray();
        var library = typeof(IHardware).Assembly;
        var provenance = ReadProvenance(library);
        // UTF-8 BOM lets Windows spreadsheet applications keep Chinese names intact.
        using var writer = new StreamWriter(csvPath, false, new UTF8Encoding(true));
        Row(writer, "Momo sensor report", "1");
        Row(writer, "SampleTimeUtc", sampledAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "Unknown");
        Row(writer, "ExportTimeUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        Row(writer, "AppVersion", (Assembly.GetEntryAssembly() ?? typeof(SensorReportService).Assembly)
            .GetName().Version?.ToString() ?? "Unknown");
        Row(writer, "Library", library.GetName().Name ?? "LibreHardwareMonitorLib");
        Row(writer, "LibraryAssemblyVersion", library.GetName().Version?.ToString() ?? "Unknown");
        Row(writer, "LibraryFileVersion", provenance.FileVersion);
        Row(writer, "LibraryBuildSHA256", provenance.Hash);
        Row(writer, "LibraryProvenance", provenance.Note);
        Row(writer, "Platform", platform);
        Row(writer, "ReadingNote", note ?? "This file contains one sample; missing/unsupported values remain empty. Min/Max are the sensor session extrema.");
        Row(writer, "PowerScopeNote", "Package, board, core, rails, battery discharge and estimates are different measurements; do not add overlapping scopes. Battery charging is not machine consumption.");
        Row(writer, "Privacy", "No host name, user name, hardware serial number or network address is collected. Export is a local file only.");
        Row(writer, "BatteryPresent", battery.IsPresent ? "true" : "false");
        Row(writer, "BatteryOnAcPower", battery.IsOnAcPower?.ToString().ToLowerInvariant() ?? "Unknown");
        Row(writer, "BatteryStatus", battery.Status);
        Row(writer, "BatterySource", battery.SourceName);
        Row(writer, "BatteryChargePercent", Number(battery.ChargePercent), protectText: false);
        Row(writer, "BatteryChargeWatts", Number(battery.ChargeWatts), protectText: false);
        Row(writer, "BatteryDischargeWatts", Number(battery.DischargeWatts), protectText: false);
        Row(writer, "BatteryReadingNote", battery.Error ?? "");
        writer.WriteLine();
        writer.WriteLine("DeviceId,DeviceName,SensorId,SensorName,Type,Unit,Scope,Value,Minimum,Maximum,State");
        foreach (var row in rows) writer.WriteLine(string.Join(",", row.Select(Escape)));
    }

    private static void Row(TextWriter writer, string key, string value, bool protectText = true) =>
        writer.WriteLine(Escape(Text(key)) + "," + Escape(protectText ? Text(value) : value));

    internal static string Number(double? value) => value is double v && double.IsFinite(v)
        ? v.ToString("G17", CultureInfo.InvariantCulture) : "";

    // CSV quoting alone does not prevent formula evaluation when opened in Excel.
    internal static string Text(string? value)
    {
        value ??= "";
        string first = value.TrimStart();
        return first.Length > 0 && first[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
    }
    internal static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    private sealed record Provenance(string FileVersion, string Hash, string Note);
    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification =
        "Development builds hash the external DLL; bundled releases use the embedded provenance manifest when Location is empty.")]
    private static Provenance ReadProvenance(Assembly library)
    {
        string version = library.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";
        string hash = "Unavailable";
        string note = "Custom upstream build; its original upstream commit was not recorded.";
        // Development DLLs can be hashed directly. Single-file releases use the bundled build manifest.
        if (!string.IsNullOrWhiteSpace(library.Location) && File.Exists(library.Location))
        {
            try
            {
                using var file = File.OpenRead(library.Location);
                hash = Convert.ToHexString(SHA256.HashData(file));
                version = FileVersionInfo.GetVersionInfo(library.Location).FileVersion ?? version;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        try
        {
            var assembly = typeof(SensorReportService).Assembly;
            string? resource = assembly.GetManifestResourceNames().FirstOrDefault(n =>
                n.EndsWith("LibreHardwareMonitor.provenance.json", StringComparison.Ordinal));
            if (resource != null)
            {
                using var stream = assembly.GetManifestResourceStream(resource);
                using var document = JsonDocument.Parse(stream!);
                var root = document.RootElement;
                if (hash == "Unavailable") hash = root.GetProperty("sha256").GetString() ?? hash;
                if (version == "Unknown") version = root.GetProperty("fileVersion").GetString() ?? version;
                note = root.GetProperty("provenance").GetString() ?? note;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IOException) { }
        return new(version, hash, note);
    }
}
