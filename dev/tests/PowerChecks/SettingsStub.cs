namespace StatusMonitor.Settings;

// Only the storage path is stubbed. All integration, history and atomic recovery code
// is production code, writing solely to this test's newly created private directory.
public static class AppSettings
{
    public static string DataDir { get; set; } = "";
    public const string PortableFolderName = "momo-data";
}
