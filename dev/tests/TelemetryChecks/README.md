# Telemetry checks

Uses the bundled LibreHardwareMonitor interfaces with DispatchProxy-only devices and a test
discovery hub. It does not open Computer, load a driver, or read/write real hardware.

Checks valid-first selection; AMD Tctl/Tdie/CCD names; exclusion of TjMax/distance/limits;
Core/Hot Spot/VRAM separation; source min/max; single GPU power domain and its scope; missing,
invalid and valid-zero readings; recursive raw reports; and failed-tree isolation.

Run with the repository's installed SDK:

```powershell
& "$env:USERPROFILE/.dotnet/dotnet.exe" run --project dev/tests/TelemetryChecks -c Release
```
