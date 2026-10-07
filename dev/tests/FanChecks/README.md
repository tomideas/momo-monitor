# Fan regression checks

Run with the .NET 8 SDK or later:

```powershell
dotnet run --project dev/tests/FanChecks/FanChecks.csproj -c Release
```

The harness links the production fan service and row view model. Its hardware hub is a
test-only implementation, and every hardware, sensor and control is an interface proxy.
Control writes only increment counters or raise a mock driver exception. The test never
opens a real hardware monitor, loads a sensing driver, or accesses user settings files.

Checks cover temperature display in Auto / fixed / curve modes, read-only monitoring,
stopped / absent RPM, delayed temperature discovery, valid sensor preference, missing source
fallback, rejected controls, failed firmware recovery retries, and GPU source identity.
