using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace StatusMonitor.Services;

/// <summary>
/// The kernel driver that CPU temperature and motherboard fans are read through.
/// <para>
/// Windows lets nothing in user mode read a model-specific register or talk to the LPC bus,
/// and administrator rights do not change that: the CPU package temperature, the package
/// power and every SuperIO fan header are behind a kernel driver, or they are behind nothing
/// at all. Tools that appear to need no driver ship their own inside their installer. This
/// one does the same, with the difference that it says so and asks first.
/// </para>
/// <para>
/// The driver is PawnIO: a signed, general-purpose IO driver that runs the hardware modules
/// LibreHardwareMonitor already carries (<c>IntelMSR</c> for the CPU, <c>LpcIO</c> for the
/// board). It replaced WinRing0, which Microsoft's vulnerable-driver blocklist now refuses to
/// load. Its own installer states that it may be redistributed unmodified, and that is what is
/// embedded here — the same file, with the same signature, nothing repackaged.
/// </para>
/// </summary>
public static class SensorDriverService
{
    /// <summary>The name shown to the user, and the folder the installer creates.</summary>
    public const string DriverName = "PawnIO";

    /// <summary>Where the official installer comes from, for anyone who would rather fetch it.</summary>
    public const string HomePage = "https://pawnio.eu/";

    internal const string ResourceName = "StatusMonitor.Drivers.PawnIO_setup.exe";

    /// <summary>
    /// Whether the driver is present on this machine. The library user mode talks to it
    /// through is the thing that has to exist, so that is what is looked for rather than a
    /// running service: the driver is loaded on demand, and a stopped one would otherwise be
    /// reported as missing and offered for installation over and over.
    /// </summary>
    public static bool IsInstalled()
    {
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            try
            {
                string root = Environment.GetFolderPath(folder);
                if (root.Length > 0 && File.Exists(Path.Combine(root, DriverName, "PawnIOLib.dll"))) return true;
            }
            catch { /* an unreadable well-known folder is not an answer, so try the next */ }
        }
        return false;
    }

    /// <summary>
    /// Runs the embedded installer with no interface of its own. Returns false if the driver
    /// did not end up installed, whatever the reason — there is a user waiting on an answer,
    /// not on a diagnosis.
    /// <para>
    /// The installer refuses to run over an existing installation, so a machine that already
    /// has the driver is success rather than something to attempt.
    /// </para>
    /// </summary>
    public static bool Install()
    {
        if (IsInstalled()) return true;

        string file = Path.Combine(Path.GetTempPath(), "momo-" + DriverName + "-setup.exe");
        try
        {
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            {
                if (source is null) return false;
                using var target = File.Create(file);
                source.CopyTo(target);
            }

            var info = new ProcessStartInfo
            {
                FileName = file,
                // -silent means no window at all, including on failure, so the exit code and
                // the check afterwards are the only things that can be believed.
                Arguments = "-install -silent",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(info);
            if (process is null) return false;
            if (!process.WaitForExit(120000))
            {
                try { process.Kill(); } catch { }
                return false;
            }
            // Both, not just the exit code: what matters here is that the machine can read its
            // sensors now, not that a process ended tidily.
            return process.ExitCode == 0 && IsInstalled();
        }
        catch { return false; }
        finally { try { File.Delete(file); } catch { /* a temp file left behind is not a failure */ } }
    }
}
