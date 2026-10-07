using StatusMonitor.I18n;
using StatusMonitor.Settings;

namespace StatusMonitor.ViewModels;

public sealed partial class MainViewModel
{
    public string DataPath => DataStorageService.Status.DataPath;
    public string StorageModeText => Loc.Instance[DataStorageService.Status.IsPortable ? "storage_portable" : "storage_standard"];
    public bool HasStorageNotice => StorageNotice.Length > 0;
    public string StorageNotice
    {
        get
        {
            var status = DataStorageService.Status;
            var key = status.Problem switch
            {
                DataStorageProblem.NotWritable => "storage_not_writable",
                DataStorageProblem.SaveFailed => "storage_save_failed",
                DataStorageProblem.RecoveredData => "storage_recovered",
                DataStorageProblem.CorruptData => "storage_corrupt",
                DataStorageProblem.MigrationFailed => "storage_migration_failed",
                _ => ""
            };
            var parts = new List<string>();
            if (key.Length > 0) parts.Add(Loc.Instance[key]);
            if (status.MachineChanged) parts.Add(Loc.Instance["storage_machine_changed"]);
            return string.Join(Environment.NewLine, parts);
        }
    }
    public void RefreshStorageStatus()
    {
        OnPropertyChanged(nameof(StorageModeText));
        OnPropertyChanged(nameof(DataPath));
        OnPropertyChanged(nameof(StorageNotice));
        OnPropertyChanged(nameof(HasStorageNotice));
    }
}
