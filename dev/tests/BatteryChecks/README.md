# Battery and sensor-report checks

Links the production battery interpretation and CSV writer. It does not enumerate hardware,
open battery handles, install drivers or issue fan/power commands. Cases cover signed mW,
unknown and relative units, multiple packs, UPS filtering, conservative platform detection,
Windows structure layouts, sample time, CSV quoting/formula protection and binary provenance.

```powershell
dotnet run --project dev/tests/BatteryChecks/BatteryChecks.csproj
```

Actual battery driver support and rate accuracy still require notebook comparison testing.
