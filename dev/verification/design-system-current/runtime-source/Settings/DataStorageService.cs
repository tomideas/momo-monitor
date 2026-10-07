using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace StatusMonitor.Settings;

public enum DataStorageProblem
{
    None,
    NotWritable,
    SaveFailed,
    RecoveredData,
    CorruptData,
    MigrationFailed,
}

/// <summary>Process-wide diagnostics for the selected data folder; no fallback changes its location.</summary>
public sealed record DataStorageStatus(
    string DataPath,
    bool IsPortable,
    bool IsWritable,
    DataStorageProblem Problem = DataStorageProblem.None,
    string Detail = "",
    string FilePath = "",
    bool MachineChanged = false);

/// <summary>
/// Settings and history use the same atomic write and recovery rules. A failed save leaves the
/// last good file intact. An unreadable file is protected from later default-value saves.
/// </summary>
public static class DataStorageService
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> ProtectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private static DataStorageStatus? _status;

    public static event Action<DataStorageStatus>? StatusChanged;

    public static DataStorageStatus Status
    {
        get
        {
            lock (Gate)
                return _status ??= new(AppSettings.DataDir,
                    string.Equals(Path.GetFileName(AppSettings.DataDir), AppSettings.PortableFolderName,
                        StringComparison.OrdinalIgnoreCase), true);
        }
    }

    /// <summary>Checks actual create/delete access, rather than assuming that an existing folder is writable.</summary>
    public static bool VerifyDataDirectory()
    {
        string probe = Path.Combine(AppSettings.DataDir, ".momo-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Flush(flushToDisk: true);
            File.Delete(probe);
            UpdateStatus(s => s with
            {
                IsWritable = true,
                Problem = s.Problem == DataStorageProblem.NotWritable ? DataStorageProblem.None : s.Problem,
            });
            return true;
        }
        catch (Exception ex)
        {
            ReportProblem(DataStorageProblem.NotWritable, AppSettings.DataDir, ex.Message);
            return false;
        }
        finally
        {
            try { if (File.Exists(probe)) File.Delete(probe); } catch { }
        }
    }

    /// <summary>
    /// Writes and flushes a temporary file beside the destination, then atomically replaces it
    /// while retaining the previous version as .bak. Returns false and publishes diagnostics on failure.
    /// </summary>
    public static bool TryWriteJson<T>(string path, T value, bool writeIndented = false)
    {
        string fullPath = Path.GetFullPath(path);
        DataStorageProblem problem = DataStorageProblem.None;
        string detail = "";
        lock (Gate)
        {
            if (ProtectedPaths.Contains(fullPath))
            {
                problem = DataStorageProblem.CorruptData;
                detail = "The unreadable data file is preserved. Restore a valid file or its backup before saving.";
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    byte[] json = JsonSerializer.SerializeToUtf8Bytes(value,
                        new JsonSerializerOptions { WriteIndented = writeIndented });
                    string temporary = fullPath + ".tmp";
                    using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                        FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        stream.Write(json);
                        stream.Flush(flushToDisk: true);
                    }
                    if (File.Exists(fullPath))
                        File.Replace(temporary, fullPath, fullPath + ".bak", ignoreMetadataErrors: true);
                    else
                        File.Move(temporary, fullPath);
                }
                catch (Exception ex)
                {
                    problem = DataStorageProblem.SaveFailed;
                    detail = ex.Message;
                }
            }
        }
        if (problem != DataStorageProblem.None)
        {
            ReportProblem(problem, fullPath, detail);
            return false;
        }
        UpdateStatus(s => s with
        {
            IsWritable = true,
            Problem = s.FilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase) &&
                s.Problem is DataStorageProblem.SaveFailed or DataStorageProblem.NotWritable
                    ? DataStorageProblem.None : s.Problem,
        });
        return true;
    }

    /// <summary>
    /// Reads the primary JSON, then its last good backup and interrupted-write file. Missing data
    /// returns null. Recovery retains the damaged original as .corrupt-*; unrecoverable files are
    /// never overwritten by a subsequent save of default values.
    /// </summary>
    public static T? ReadJsonWithRecovery<T>(string path, Func<T, bool>? validate = null) where T : class
    {
        string fullPath = Path.GetFullPath(path);
        T? recovered = null;
        string recoveredFrom = "";
        string detail = "";
        bool found = false;
        bool primaryFailed = false;
        bool restoreBlocked = false;
        lock (Gate)
        {
            foreach (string candidate in new[] { fullPath, fullPath + ".bak", fullPath + ".tmp" })
            {
                if (!File.Exists(candidate)) continue;
                found = true;
                try
                {
                    T value = JsonSerializer.Deserialize<T>(File.ReadAllText(candidate))
                        ?? throw new JsonException("The JSON file does not contain data.");
                    if (validate is not null && !validate(value))
                        throw new JsonException("The JSON data is invalid.");
                    if (candidate == fullPath)
                    {
                        ProtectedPaths.Remove(fullPath);
                        return value;
                    }
                    recovered = value;
                    recoveredFrom = candidate;
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            string quarantine = fullPath + ".corrupt-" +
                                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
                            File.Move(fullPath, quarantine);
                        }
                        catch (Exception ex)
                        {
                            // Reading a valid backup must still work on read-only media. Keep
                            // the recovered values in memory and protect the damaged original.
                            restoreBlocked = true;
                            detail = ex.Message;
                            ProtectedPaths.Add(fullPath);
                        }
                    }
                    // Recovering .tmp must leave that candidate available until its value is in memory.
                    if (!restoreBlocked) ProtectedPaths.Remove(fullPath);
                    break;
                }
                catch (Exception ex)
                {
                    primaryFailed |= candidate == fullPath;
                    detail = ex.Message;
                }
            }
            if (recovered is null && found) ProtectedPaths.Add(fullPath);
        }
        if (recovered is not null)
        {
            if (restoreBlocked)
            {
                ReportProblem(DataStorageProblem.NotWritable, fullPath, detail);
                return recovered;
            }
            bool restored = TryWriteJson(fullPath, recovered);
            // A failed restore is already visible as a save failure; the recovered values are still usable.
            if (restored)
                ReportProblem(DataStorageProblem.RecoveredData, fullPath, recoveredFrom);
            return recovered;
        }
        if (found)
            ReportProblem(DataStorageProblem.CorruptData, fullPath,
                primaryFailed ? detail : "No valid data or backup could be loaded.");
        return null;
    }

    public static void ReportMachineChange() => UpdateStatus(s => s with { MachineChanged = true });

    public static void ReportProblem(DataStorageProblem problem, string path, string detail) =>
        UpdateStatus(s => problem == DataStorageProblem.RecoveredData &&
            s.Problem is DataStorageProblem.NotWritable or DataStorageProblem.SaveFailed or
                DataStorageProblem.CorruptData or DataStorageProblem.MigrationFailed
            ? s
            : s with
            {
                Problem = problem,
                FilePath = path,
                Detail = detail,
                IsWritable = problem is DataStorageProblem.NotWritable or DataStorageProblem.SaveFailed ? false : s.IsWritable,
            });

    private static void UpdateStatus(Func<DataStorageStatus, DataStorageStatus> update)
    {
        DataStorageStatus next;
        lock (Gate)
        {
            var previous = Status;
            next = update(previous);
            if (next == previous) return;
            _status = next;
        }
        var handlers = StatusChanged;
        if (handlers is null) return;
        foreach (Action<DataStorageStatus> handler in handlers.GetInvocationList())
            try { handler(next); } catch { /* UI diagnostics must not interrupt monitoring or persistence. */ }
    }

    /// <summary>A local, one-way machine marker. No hostname, registry identifier or user name is stored.</summary>
    internal static string CurrentMachineId()
    {
        string identity = Environment.MachineName;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                identity = key?.GetValue("MachineGuid") as string ?? identity;
            }
            catch { /* A machine name still gives a safe conservative boundary when registry access is denied. */ }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("MomoMonitor:" + identity)));
    }
}
