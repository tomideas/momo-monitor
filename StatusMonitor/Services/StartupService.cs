using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

namespace StatusMonitor.Services;

/// <summary>
/// Starting with Windows, as a logon task rather than a Run key.
/// <para>
/// The app is manifested <c>requireAdministrator</c> — temperatures, fan speeds and CPU
/// package power are unreadable without it — and Windows will not launch an elevated program
/// from the Run key or the Startup folder. Such an entry does not quietly work: it is either
/// dropped or left waiting behind a consent prompt at every sign-in, which is the one thing
/// an option called "start with Windows" must not do. A logon task running with the highest
/// available privileges is the supported way to start an elevated program at sign-in without
/// prompting, and it is what every other sensor tool on Windows uses.
/// </para>
/// <para>
/// Registered through <c>schtasks.exe</c> with a definition file. The COM interface would
/// avoid spawning a process, but it is late-bound from C# and fails in ways that are hard to
/// tell apart from one another; schtasks answers with a single exit code.
/// </para>
/// </summary>
public static class StartupService
{
    /// <summary>The task's name in the root folder of the Task Scheduler library.</summary>
    public const string TaskName = "MomoMonitor";
    public const string TrayArgument = "--tray";

    /// <summary>Only an explicit background launch bypasses the splash and panels.</summary>
    public static bool StartsInTray(IEnumerable<string> arguments) => arguments.Contains(TrayArgument, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Seconds between signing in and the app starting. Sign-in is the busiest minute a desktop
    /// has — shell, drivers, every other startup item — and this one opens hardware monitoring
    /// drivers and takes a first full sample before it shows anything. Standing aside for a
    /// moment costs nothing a monitor cares about: the energy total accumulates from readings,
    /// so the figures simply start when sampling starts.
    /// </summary>
    public const int LogonDelaySeconds = 20;

    /// <summary>The account the task belongs to and runs as: this one, never "all users".</summary>
    internal static string CurrentUserId() =>
        string.IsNullOrEmpty(Environment.UserDomainName)
            ? Environment.UserName
            : Environment.UserDomainName + "\\" + Environment.UserName;

    /// <summary>
    /// The task definition, which is where everything this option promises is actually stated.
    /// Kept as text with no dependency on a scheduler, so what gets registered can be read and
    /// tested rather than inferred from a pile of property assignments.
    /// </summary>
    internal static string BuildTaskXml(string exePath, string userId)
    {
        string command = SecurityElement.Escape(exePath) ?? "";
        string folder = SecurityElement.Escape(Path.GetDirectoryName(exePath) ?? "") ?? "";
        string user = SecurityElement.Escape(userId) ?? "";
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>{user}</Author>
                <Description>Starts Momo System Monitor when {user} signs in. Created by the app; clear "Start with Windows" in its settings to remove it.</Description>
                <URI>\{TaskName}</URI>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                  <Delay>PT{LogonDelaySeconds}S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>false</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                  <Arguments>{TrayArgument}</Arguments>
                  <WorkingDirectory>{folder}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    /// <summary>Whether a task by this name exists at all, whatever it points at.</summary>
    public static bool IsRegistered() => RunSchtasks("/Query", "/TN", TaskName);

    /// <summary>
    /// Registers, or re-registers, the logon task for the executable at <paramref name="exePath"/>.
    /// Always writes the whole definition: this is also the path that repairs a task left
    /// pointing at an executable that has since been moved, which a single portable file
    /// invites, and rewriting is cheaper than reading the old one back to compare.
    /// </summary>
    public static bool Enable(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        // One fixed name rather than a random one, so a crash cannot leave a pile behind.
        string file = Path.Combine(Path.GetTempPath(), "momo-startup-task.xml");
        try
        {
            // UTF-16 with a byte-order mark. schtasks reads the file as Unicode and rejects a
            // definition it cannot identify, and the declaration inside says UTF-16.
            File.WriteAllText(file, BuildTaskXml(exePath, CurrentUserId()), new UnicodeEncoding(false, true));
            return RunSchtasks("/Create", "/TN", TaskName, "/XML", file, "/F");
        }
        catch { return false; }
        finally { try { File.Delete(file); } catch { /* a temp file left behind is not a failure */ } }
    }

    /// <summary>
    /// Removes the logon task. Nothing to remove counts as success: what was asked for is
    /// "does not start with Windows", and that is already true.
    /// </summary>
    public static bool Disable() => RunSchtasks("/Delete", "/TN", TaskName, "/F") || !IsRegistered();

    /// <summary>Applies the preference to this machine, for the executable that is running.</summary>
    public static bool Apply(bool enabled) =>
        enabled ? Enable(Environment.ProcessPath ?? "") : Disable();

    private static bool RunSchtasks(params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                // The full path: this must run schtasks, not whatever is first on PATH.
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                // Redirected so no console window flashes over the dashboard, and then drained
                // so a full pipe cannot leave schtasks blocked on a reader that never reads.
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process is null) return false;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (process.WaitForExit(15000)) return process.ExitCode == 0;
            try { process.Kill(); } catch { }
            return false;
        }
        catch { return false; }
    }
}
