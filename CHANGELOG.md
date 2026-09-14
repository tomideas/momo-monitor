# Changelog - Momo Monitor

## [0.1.2] — 2026-09-14

### Added
- **Power at the wall** — optional "wall meter" mode for the hero number: adds the PSU's own losses (80 PLUS class + rated wattage, both printed on the supply's label) on top of component draw. Off by default.
- **Manual board power for unrecognised GPUs** — a card that can't report its power limit used to be excluded from the total; a settings field now appears exactly when one is detected, so you can type its board power from the spec sheet and have it counted.

### Changed
- **GPU idle-power model rewritten** — the flat "10% of rating" floor is replaced by a fixed base + slope that follows published idle figures (≈5–7 W for a 47 W card, ≈20–25 W for a 450 W card); top-end cards were previously overstated by about 2× at idle.
- **New app icon** — `momo.png` replaces `momo_pop.png`.
- FeatureChecks regression suite extended (+130 lines).

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
