# Momo Monitor startup contract

The product uses native WPF. Authoritative design data and mappings remain in
`design-system/design-system.json`; this index records the startup workflow tested
in this task. Web-only audit rules do not validate native WPF behavior.

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
|---|---|---|---|---|
| Startup animation | SplashWindow | design-system/design-system.json; Assets/startup.webp | single playback; reduced motion shows final frame | dev/tests/SplashChecks/run.ps1 |
| Windows logon | StartupService; App.Launch; MainWindow; TrayService | Windows scheduled task Arguments=--tray | background start; dashboard restore; tray Exit | dev/tests/FeatureChecks; dev/tests/TrayStartupChecks |
| Tray menu | TrayService; MainWindow | Loc; design-system/design-system.json | Open Dashboard / Mini / Settings / separator / Exit; localized labels; Settings restores dashboard and opens existing overlay | dev/tests/TrayStartupChecks |
| Settings | MainWindow.Settings; existing SettingsOverlay | AppSettings; Loc; design-system/design-system.json | General / Monitoring & alerts / Data & Info; OK commits validated draft; Cancel, X and Escape discard it; confirmed Reset totals runs only on OK | native --verify-settings |
| Process inspection | MainWindow.SelectView; RevealInspection | MainViewModel inspection/trends/process ranking | Dashboard has summary metrics; Process owns Trends and Top Processes; CPU/GPU/RAM/DISK and alert drill-downs navigate to Process; fans appear only on Fans | native --verify-features |

Start sensor warm-up and mascot playback concurrently. Display the dashboard only
after both complete. Keep the final frame while sensor work continues; the loading
sweep remains independent. Closing the splash always stops its frame timer and
resolves animation completion. Preserve theme resources and existing translations.

The splash contract applies to manual launch. A Windows logon launch uses `--tray`
and creates the monitoring dashboard without showing any window. Keep its tray icon
visible and sampling active. OnMainWindowClose binds lifetime to that hidden dashboard;
tray Exit closes it normally. Restoring opens the dashboard without the manual-launch
mini-mode hop. Re-registering an enabled startup preference migrates older tasks to
the background argument.
