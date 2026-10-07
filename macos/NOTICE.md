# Sources and notices

Momo macOS source is part of Momo Monitor and follows the root `../LICENSE`.

The approved Momo artwork and animation frames are copied unchanged from this
repository's `StatusMonitor/Assets/`. The generated resource manifest records
the source paths, SHA-256 fingerprints, frame counts and durations.

Fonts are the same embedded fonts used by the Windows product:

- Geist / Geist Mono — Vercel; SIL Open Font License 1.1.
- Barlow Condensed — Jeremy Tribby; SIL Open Font License 1.1.
- Noto Sans TC — Google / Adobe contributors; SIL Open Font License 1.1.

The font binaries retain their embedded copyright and license metadata.
License texts copied from the upstream font repositories are in `Licenses/`.

Architecture reference: Acerola-1/hagimi-monitor, reviewed at commit
`17e306e609e39871a0dedb42e731d31b995bb684` (AGPL-3.0). No source file or artwork
from that repository is incorporated here. It informed the distinction between
system power, DC input and battery charging, and the choice to omit IOReport.
SMC keys and message field layouts are hardware interface facts; the read-only
C adapter in this project is independently implemented.
