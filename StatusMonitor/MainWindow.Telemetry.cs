using System.Windows;
using StatusMonitor.I18n;

namespace StatusMonitor;

public partial class MainWindow
{
    internal void PreviewTelemetry(bool charging = false)
    {
        _vm.PreviewTelemetry(charging);
    }

    internal string VerifyTelemetryInteractions(bool charging)
    {
        if (!_preview) throw new InvalidOperationException("Telemetry checks require --render.");
        int count = 0;
        void Check(bool condition, string message) { count++; if (!condition) throw new InvalidOperationException(message); }
        UpdateLayout();
        Check(BatteryTelemetry.Visibility == Visibility.Visible && _vm.HasBattery, "Portable battery telemetry is hidden");
        Check(_vm.BatteryRateText.Contains(charging ? "18.5" : "24.2"), "Signed battery rate has wrong direction/value");
        Check(_vm.TotalWattsValue == (charging ? "17.6" : "24.2"), "Charging rate replaced computer power");
        Check(_vm.PowerLabel == Loc.Instance[charging ? "power_cpu_gpu" : "battery_discharge"], "Power scope label does not match the reading");
        Check(_vm.TemperatureSourceText.Contains("CPU Package"), "Temperature selection lost source name");
        Check(_vm.SensorDetails.Any(s => s.Value.Contains("59.0") && s.Range.Contains("76.0")), "Sensor min/max details missing");
        Check(_vm.PowerSourceText.Contains(Loc.Instance["scope_cpu-package"]), "CPU power scope missing");
        Check(_vm.EnergyCoverageText.Length > 0, "Energy coverage metadata unavailable");
        Check(!_vm.CanPersist && !_vm.Fans.Targets.Any(f => f.SoftwareControlled), "Preview wrote settings or controlled a fan");
        return $"PASS: {count} native telemetry assertions (battery direction, power scope, sources, min/max, coverage, read-only preview).";
    }
}
