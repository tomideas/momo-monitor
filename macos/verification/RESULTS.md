# macOS initial draft verification — 2026-10-07

Environment: Apple Silicon, macOS 27.0.1, Apple Swift 6.4 Command Line Tools.
The application targets macOS 15.0. These results do not establish support on
other hardware or older macOS versions.

## Completed

- Native release compilation using `bash build.sh`, assembled `.app` and local
  ad-hoc signature. `codesign --verify --deep --strict` passed.
- 13/13 core checks: trapezoidal Wh, missing samples, real zero, source/scope
  transitions, sleep/reset, clock changes, midnight split, invalid values,
  restart baseline, signed current, separate daily scopes, complete tariff
  validation and corrupt-history rejection. See `core-checks.txt`.
- Six live SMC PSTR samples over approximately 10 seconds, ranging from 5.65 W
  to 11.47 W during this run. The accumulated system-scope energy was
  0.0250899 Wh over 10.045 seconds. See `live-probe.ndjson`. This verifies live
  access and accumulation, not calibrated wall-power accuracy.
- 24/24 native checks: real sampling/energy, both themes and locales, dashboard,
  Mini and settings snapshots, hidden/static animation, frame progression,
  no hover restart, drag clip priority, single-shot completion, sleep/wake,
  preferences/history persistence and preservation of corrupt history.
  See `native-checks.json` and `native-run.log`.
- Actual AppKit/SwiftUI screenshots inspected: `paper-en.png`, `volt-zh.png`,
  `paper-en-mini.png`, `volt-zh-mini.png`, and both settings screenshots.
  Settings use the shared Momo surface; native fields retain platform chrome.
- Canonical design JSON values are unchanged. Native component, shared mappings
  and verification were added; generated design documents were refreshed.

The first animation check exposed per-frame scheduling drift. Playback now uses
absolute monotonic deadlines, and the single-shot completion check passes.

## Remaining validation

The Computer Use service timed out when selecting the built application. Actual
pointer dragging, double-click restoration, menu interaction, native picker
popup and a full keyboard/focus pass were therefore not completed through that
tool. Programmatic native animation and window rendering checks are complete,
but they do not substitute for these interactions.

Intel, macOS 15/26, additional Apple Silicon models, battery discharge/charging
on a physical MacBook, stale registry refresh behavior, external power-meter
comparison and App Store sandbox compatibility remain unverified.

The app has no login-item registration, auto-update, IOReport CPU/GPU energy,
fan control or per-process watts. It is locally ad-hoc signed, not Developer ID
signed/notarized, and is not a public distribution build.
