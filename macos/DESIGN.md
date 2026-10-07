# Momo macOS design context

The only editable design source is `../design-system/design-system.json`, governed
by `../design-system/AI.md`. This file records the macOS implementation and does
not define a second palette. Windows mappings remain intact.

Momo's job here is energy awareness with a capybara companion. Keep PAPER POP and
VOLT, the condensed italic power reading, Geist UI text and Geist Mono data.
The signature is the existing interactive mascot beside a clearly scoped watt
reading. Avoid adding sensor pages, glass themes or decorative gauges.

`Tools/prepare-resources.py` reads the current canonical JSON and generates
`Sources/MomoMac/Resources/DesignTokens.json`. `Sources/MomoMac/Design.swift`
adapts its semantic colors, spacing, radii, control height and font families to
SwiftUI. Regenerate through `build.sh`; never edit the snapshot independently.
The 92 pt dashboard reading and 36 pt Mini reading are platform component values,
not replacements for the shared display-size token. The authoritative JSON
records this native variant and its verification.

AppKit owns the menu-bar item, popover and floating window. SwiftUI owns their
contents. Native pickers, text fields, keyboard focus and their popups are
deliberately platform-owned. Product surfaces still consume Momo theme tokens;
settings use the shared surface rather than an unrelated system-white panel.

The three animations are copied unchanged from the approved Windows assets.
Their PNGs, transparency, dimensions, registration and frame timing are kept.
Playback is event-driven, occurs once, uses absolute monotonic deadlines and
stops on hide, close, sleep, reduced motion or Low Power Mode. The menu-bar icon
is static. Opening the energy panel never waits for startup animation to finish.

The initial interface uses a 400 pt energy panel and a 278 pt Mini window. Power
and today’s energy lead; CPU and memory are supporting context. Color does not
indicate arbitrary hardware categories. Missing readings remain an em dash.
