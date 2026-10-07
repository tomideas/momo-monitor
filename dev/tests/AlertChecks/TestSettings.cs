namespace StatusMonitor.Settings;

// No hardware, UI, portable folder discovery, or user's real settings are used by these checks.
public sealed class AppSettings
{
    public const string PortableFolderName = "momo-data";
    public static string DataDir { get; } = System.IO.Path.Combine(AppContext.BaseDirectory,
        "test-data", Guid.NewGuid().ToString("N"));
    public int EffectiveRefreshSeconds { get; set; } = 2;
    public int EffectiveIdleRefreshSeconds { get; set; } = 5;
    public int AlertHoldSeconds { get; set; } = 15;
    public int AlertCooldownSeconds { get; set; } = 300;
}
