# Changelog - Momo Monitor

## [0.1.6] — 2026-10-07

### Added
- Named raw sensor details and user-requested local CSV export, including sample/export timestamps, units, power/temperature scope, current/min/max values, read state, app version and bundled LibreHardwareMonitor binary fingerprint. Text cells are protected from spreadsheet formula interpretation.
- Read-only Windows battery telemetry with AC state, charge percentage and separate charging/discharging W. Relative units, unknown rates, incomplete enumeration and mixed battery directions remain unavailable. Multi-pack aggregation excludes UPS devices and requires complete compatible readings.
- Conservative desktop/portable/unknown classification and a documented hardware comparison matrix. Notebook accuracy remains pending physical cross-computer verification.

### Fixed
- Invalid preferred temperatures no longer prevent valid fallbacks; temperature distance-to-limit is excluded from absolute temperature selection. CPU monitoring/fan source selection shares the resolver; GPU core, hot spot and memory junction remain separate readings.
- Whole-GPU power no longer sums overlapping board/core/rail sensors. CPU package totals do not add integrated-GPU power again, and a valid reported zero does not trigger an idle-power estimate.
- Notebook and unknown-platform power no longer inherit desktop model defaults. Battery charging never enters consumption; supported off-AC discharge has a distinct battery-side scope.
- Energy integration breaks at suspend/resume, missing power, source changes and long sampling gaps. Sleep/hibernate hours are not charged at the last awake wattage. Monitoring coverage/source metadata is added without rewriting older energy figures.
- A failed hardware tree no longer invalidates healthy device readings; failed/stale sources cannot continue custom fan control.
- The default build prepares only the root executable. Portable folder/ZIP generation now requires an explicit `-Portable` option.

## [0.1.5] — 2026-10-07

### Added
- **Complete fan readings** — Auto, fixed-speed and temperature-based fans all retain monitoring temperature and identify its source. Each row separates firmware control, applied custom control, pending control and failures; a missing RPM is distinct from a reported 0 RPM.
- **Inspect from the dashboard** — select CPU or GPU to reveal temperature trends, RAM to inspect usage and rank processes by memory, or DISK to inspect processes ranked by disk activity. Inspecting a second GPU does not change the primary GPU used by alerts. Process ranking is also selectable by power, CPU, GPU, memory or disk.
- **Explain unavailable readings** — the dashboard reports stale readings, insufficient privileges, unavailable hardware sensing or a missing CPU sensor driver, with routes to retry or monitoring settings. Fans explain unsupported sensors and temporary missing readings; missing data remains separate from zero usage.
- **Persistent alert events** — a dedicated Alerts page retains the last seven days across restarts, with start/end, duration, threshold, peak (or lowest free space), active/recovered/interrupted state and acknowledgement. Tray notifications open their event; related readings and alert settings are accessible from the page. Restart and sampling gaps interrupt an incident rather than inventing unobserved duration.
- **Ready-to-share portable distribution** — the standard build includes the .NET runtime in one Windows x64 executable. It creates a clean folder and zip containing `MomoMonitor.exe`, a bilingual guide, `LICENSE` and an empty `momo-data`; local personal data is excluded. Settings shows the actual data folder, write checks, save failures, backup recovery and computer-change notices.

### Fixed
- Auto fan mode no longer clears monitoring temperature, and a failed custom command is no longer presented as successfully applied.
- Sustained-alert timing accommodates the configured foreground/background sampling interval, preventing the normal five-second background cadence from repeatedly resetting the hold period.
- Settings, energy totals/history and alert history use atomic saves with a last-good `.bak`. Damaged originals are preserved; data that cannot be recovered is protected from being overwritten with defaults. A read-only portable folder never silently falls back to AppData.

### Changed
- Moving portable data to a new or unverified computer retains general preferences and histories, while returning fan control to Auto and resetting hardware calibration, startup preferences and window placement. Legacy settings without a machine marker receive this safety reset once.
- Builds restore through the repository's NuGet configuration and an isolated workspace AppData/package cache. Test and framework-dependent builds have separate package names; populated distribution folders are protected from replacement.

## [0.1.4] — 2026-09-27

### Fixed
- **Black dashboard on a machine with no monitor attached** — on a headless desktop (one reached over remote control with no display plugged into the GPU), WPF's hardware rendering path painted only the title bar and left the whole window black; the process stayed alive and responsive, so it looked like the app had hung when only its content was missing. Its sensors were never at fault — a native monitor read the same hardware fine. The trigger gives nothing to branch on: WPF still reported full render capability and Windows still enumerated a virtual 1600×900 monitor, so the failure could not be detected before it happened. The app now starts in WPF software rendering, which paints correctly in every case and is more than fast enough for a dashboard that redraws once a second.

## [0.1.3] — 2026-09-24

### Added
- **Name your fans, and choose what each one follows** — a board reports its headers as "Fan #1", "Fan #2" and nothing else, so the Fans page gave no way to tell a CPU fan from a chassis one. A row's name is now editable in place, and a "Follow: Auto / CPU / GPU / Hotter of CPU & GPU" choice sits beside the curve. This is the freedom Fan Control gives — the owner says what a header is, rather than the app guessing it from a name it was never given. Renaming works while a fan is still on Auto, a name picks the row's glyph (a header called "CPU Fan" is drawn as one), and a fan nobody has touched behaves exactly as before.

### Changed
- **A GPU fan is shown even when stopped** — the Fans page hides a 0 RPM row so an idle or empty header does not crowd it, but that also hid GPU fans sitting in their own zero-fan mode. 0 RPM on a card is a healthy card idling, not an empty slot, so a GPU fan is now never hidden; board headers and the "Show all fans" toggle behave exactly as before.

## [0.1.2] — 2026-09-14

### Added
- **Power at the wall** — optional "wall meter" mode for the hero number: adds the PSU's own losses (80 PLUS class + rated wattage, both printed on the supply's label) on top of component draw. Off by default.
- **Manual board power for unrecognised GPUs** — a card that can't report its power limit used to be excluded from the total; a settings field now appears exactly when one is detected, so you can type its board power from the spec sheet and have it counted.
- **Start with Windows** — a new option under Settings → General. The app is manifested `requireAdministrator`, and Windows will not start an elevated program from the Run key or the Startup folder: the entry is either dropped or left waiting behind a consent prompt at every sign-in. So the option registers a Task Scheduler logon task instead — highest available privileges, twenty seconds after sign-in, no prompt. Clearing the box removes the task; if Windows refuses the change (running unelevated, for instance) the checkbox goes back to what is actually true and says why. Every start re-registers the task, so moving or replacing the exe never leaves a startup entry pointing at nothing.
- **Full hardware sensing without going to find a driver** — CPU temperature, CPU package power and board fan speeds are readable only through a kernel driver, which is why a fresh machine showed dashes for them. Momo now carries the official signed PawnIO installer and offers it once, on the first start of a machine that needs it; installing is silent, needs no Windows restart, and Momo reopens itself. Declining is remembered, and the offer stays on the monitoring page while the driver is missing.
- **Show all fans** — the Fans page lists the fans that are actually spinning, with a checkbox for the rest; a machine where nothing is turning says so instead of showing an empty page.

### Changed
- **Fans are found again on every hardware update** — discovery ran once after the sensor hub opened, so a USB fan hub plugged in later, or a driver that rebuilt its sensors after waking, was never seen. Rows that are still the same keep their identity, so an open editor keeps its draft. A tachometer is now paired with its control by a normalised name rather than by sensor index, which is not a channel number on the NVAPI fallback — a Quadro P1000 reports its tachometer as "GPU" at index 1 and its control as "GPU Fan" at index 0.
- **The fan row says less and shows more** — name, measured RPM and mode on one line, with the hardware details moved into the row's tooltip.
- **GPU idle-power model rewritten** — the flat "10% of rating" floor is replaced by a fixed base + slope that follows published idle figures (≈5–7 W for a 47 W card, ≈20–25 W for a 450 W card); top-end cards were previously overstated by about 2× at idle.
- **New app icon** — `momo.png` replaces `momo_pop.png`.
- `--diag` prints each sensor's index and identifier, which is what made the tachometer pairing above diagnosable.
- FeatureChecks regression suite extended (+130 lines).

### Fixed
- The README's `--diag` example passed `--out`, which only `--dump` accepts. `--diag` takes its path positionally, so the documented command wrote a file called `--out`.

## [0.1.1] — 2026-09-14

### Changed
- **Language label unified to "中文"** — the language-switch label in the README and user guide now reads "中文" instead of "繁體中文".

## [0.1.0] — 2026-09-14

### Added
- **Initial release** — Windows 11 system-monitoring widget (WPF .NET 8, runs as administrator): CPU / GPU / RAM / network / storage, fans and AIO pumps, wattage, cumulative energy, carbon footprint, electricity cost and top processes, updated every second.
- **VOLT / PAPER POP skins** — hero number (current power draw) + a shared-baseline bar list; color only ever means "over budget". Old 7-background configs map automatically.
- **Trends** — 60 s / 15 min ranges for CPU/GPU load, RAM, VRAM, temperatures and estimated power, with min / avg / max.
- **Mini window** — 258 × 238 glance panel, draggable from anywhere, double-click to return to the main window, optional pin.
- **Over-limit alerts** — defaults: CPU 90 °C, main GPU 85 °C, RAM/VRAM 90 %, fixed disk under 10 % free; 15 s sustained to fire, 300 s cooldown; in-app log keeps the last 20 alerts.
- **Bilingual** — English / 中文, switchable in settings.
- **Portable mode** — a `momo-data` folder next to the exe keeps settings and energy totals with the app; first launch migrates existing `%APPDATA%` data automatically.
- **Info page** — hardware & system details via WMI; fixed drives in one card (one row per disk), copyable spec sheet.

### Improved
- **DISK row now shows space used per drive** (used / total) instead of the misleading `% Disk Time`.
- **Secondary text contrast** raised above the WCAG AA small-text floor (6.73:1 / 6.59:1 across the two skins).
- **Native title bar tinting** — recolours with the skin instantly, keeping Windows 11 Snap Layouts and DPI behavior.
- **24 DIP two-tone vector icons** per row (CPU / GPU / RAM / DISK / NET), recolored automatically per skin.
- Embedded **Barlow Condensed Black Italic** display font (tabular figures — no jitter on per-second updates).

### Fixed
- Light-skin accent identical to ink (no accent), info-card title rendered as a black bar, `PopPaper` dot grid covered by a solid fill, hardcoded colors in `TextBox` / `ComboBox` / `TabItem`, custom ComboBox template showing anonymous objects, selected-tab hover making text invisible, and related issues.
- Settings fields (electricity price / carbon intensity / process count) not wired to save events.
