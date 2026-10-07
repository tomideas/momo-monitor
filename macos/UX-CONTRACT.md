# Momo macOS draft behavior

Source requirements: the user's macOS request, the root AGENTS.md,
`../design-system/design-system.json`, and the existing Windows energy integrator
and Mini interaction contract. The Mac edition is read-only hardware monitoring.

| Capability | Owner | Behavior | Verification |
|---|---|---|---|
| Navigation | AppController | Click menu bar to toggle energy panel; right-click offers panel, Mini, settings and Quit. | Native UI and pointer checks |
| Popover layout | AppController.prepareDashboard; DashboardView | Measure content before positioning on the menu-bar screen. Bound height to the visible screen minus 48 pt; the single native vertical scroller keeps all contents reachable on short screens. | Real popover geometry and short-height scroll checks |
| Mini | AppController; MiniGestures | Drag the body; double-click opens energy panel; arrow opens it without dragging; X hides Mini while sampling continues. | Native pointer checks |
| Animation | MascotPlayer | Idle pointer movement plays once; active playback ignores hover; drag overrides; keyboard/accessibility activation can play; hidden and reduced-motion states stop playback. | Native animation checks |
| Form | SettingsView; TariffInput | Save validates and commits; Cancel, Escape and window close discard the draft. Blank tariff disables cost, zero is valid, invalid full input is rejected. | Core checks and native interactions |
| Select/Listbox | SwiftUI Picker | OS-owned popup, keyboard, focus and geometry. Choices are PAPER POP/VOLT and English/繁體中文. | Native open-popup checks |
| Power states | TelemetryReader | Preserve system, battery and DC input boundaries. Charging is separate. No TDP or charger-rating substitution. | Live probe and core checks |
| Persistence | MonitorStore | Atomic JSON in Application Support/MomoMonitorMac. Save history every 30 seconds and on quit/sleep. Keep unreadable history intact; show failure and allow retry. | Core checks; native store checks |
| Trend | PowerTrendView | Session-only last five minutes; missing readings, long gaps, scope and source changes break the line. | Native screenshots |

The app samples every two seconds on a serial utility queue. CPU is a delta of
system CPU ticks; memory is active + wired + compressed pages / physical memory,
not memory pressure. These definitions appear in control help.

Wh uses trapezoidal integration of consecutive valid readings. Clock jumps,
sleep/wake, timezone changes, restarts and long gaps reset the baseline. Each
calendar day and measurement boundary keeps its own bucket; the panel shows the
current boundary's total. It does not combine battery and system totals.

Cost = displayed Wh / 1000 × the user's price per kWh, with the user's currency
symbol. No market tariff is assumed. All displayed costs are estimates for that
measurement boundary, not a utility bill. Monitored duration is shown alongside
energy so partial-day sampling is visible.

All product strings and accessible action names use the chosen English/繁體中文
locale. Native controls own focus and keyboard mechanics. Verification mode does
not persist settings, history or Mini position, and is separate from normal use.
