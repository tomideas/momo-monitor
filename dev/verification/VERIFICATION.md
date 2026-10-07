# Momo Monitor 0.1.5 verification — 2026-10-07

The requested changes cover fan information, dashboard inspection, missing-reading explanations, persistent actionable alerts and portable delivery. No real fan overrides, driver installation or Windows startup registration were performed during verification. Existing user data was not used as a packaging input.

## Build and logic

- SDK: user-local .NET SDK 9.0.318; target net8.0-windows / Windows x64. Release WPF build succeeded with zero warnings/errors.
- `FeatureChecks`: 431 assertions passed, including ranking the complete process sample, machine-scope reset, atomic backup/recovery and preservation of unrecoverable data.
- `AlertChecks`: 44 assertions passed, including configured sampling intervals, gaps, lifecycle, restart, retention, acknowledgement, reactive summaries and navigation.
- `FanChecks`: 36 assertions passed using mock controls; Auto/fixed monitoring, temperature selection, missing/zero readings, failed writes/releases and recovery through new driver handles.
- `HubChecks`: 13 assertions passed using a mock Computer; update failure, stale sensor suppression and reopening.
- Test commands: user-local `dotnet run --project dev/tests/<name>/<name>.csproj -c Release --no-restore` for the four projects. Build AppData was isolated under `dev/.build-env/AppData`.

## Native WPF

- `--render --verify-features --width 460 --height 760` passed 152 UI assertions in English PAPER POP and Chinese VOLT. Results: `final-check-paper.png.checks.txt` and `final-check-volt.png.checks.txt`.
- Representative screenshots cover fan Auto and custom panels, alert detail and empty state, settings/storage, 460×330 scrolling and 1040-DIP trends. Layouts were visually reviewed.
- WMI denied access in the restricted environment; startup now degrades gracefully while available OS/GPU readings continue. CPU temperature and motherboard fan control on the user's specific hardware were not tested live.
- Alert screenshots use synthetic events confined to preview memory. Preview disables persistence and hardware control.

## Portable package

- `./build.ps1 -Test` and `./build.ps1` succeeded. Final standard package uses the production administrator manifest and a self-contained single-file executable; test package differs in its asInvoker manifest.
- The packaged test executable was launched directly, without `dotnet.exe`, and passed 152 native UI assertions (`portable-native-check.png.checks.txt`).
- `portable-storage.png` shows the actual adjacent `momo-data` path. Preview left that folder empty before and after execution.
- Standard ZIP has exactly four entries: executable, bilingual guide, GPL license and empty `momo-data/`; no settings, totals or history files. Root executable and the distribution executable match.
- Full .NET packaging increases the executable/package size; no .NET installation is required by recipients. Sensor-driver and startup registration remain local to each PC.

## Design

- Formal `design-system/design-system.json` was atomically updated, reread and validated with the project's schema. Existing tokens, definitions, themes, project identity and unrelated fields were preserved.
- Shared component source/mappings and verification cover the five changes. Derived documents were generated through the project's document generator.
- The premium static audit was scoped to `StatusMonitor` and reported no web-source findings. Its scanner does not verify WPF XAML; native checks and visual review provide the WPF evidence. Editor-app and website findings from the initial whole-workspace scan are outside this task.
