# Momo Monitor design context

The authoritative design data is `design-system/design-system.json`, governed by
`design-system/AI.md`. This document is an implementation index, not a second token
store. WPF runtime colors come from `StatusMonitor/Views/Theme.cs`; typography and
shared styles live in `StatusMonitor/App.xaml`.

Momo is a Windows desktop system monitor. Keep its PAPER POP and VOLT identities,
embedded Geist / Geist Mono typography, Barlow Condensed brand name, compact
geometry and existing translated status labels. Product clarity takes priority.

The splash owns startup animation in `StatusMonitor/SplashWindow.xaml(.cs)`.
Its mascot is `Assets/startup.webp`, converted from the supplied GIF by
`dev/convert-startup-animation.py`. Only edge-connected white is removed; enclosed
white remains. A single shared crop preserves frame registration. WPF plays PNG
frames decoded from that WebP, embedded in `Assets/startup.frames.zip`.
Playback occurs once, holds the final frame and completes before the dashboard
appears. Sensor warm-up runs concurrently. Reduced motion shows the final frame
immediately. Close stops the frame timer. The existing sensor-loading sweep remains
active while loading; it is independent of the mascot's one-shot animation.

Settings footer and app/tray icons use the approved `icon_v2.png`, copied unchanged
to `Assets/momo.png`. Windows ICO sizes use a transparent square canvas without
distorting the original. The bottom settings credit reads
`Designed by Tom Tam · tomideas.com · Version 2.22.32`, with a bold, clickable website
and version read from the assembly. Reuse the existing
theme resources, motion policy and locale provider; avoid adding startup-specific
colors, changing brand typography or redesigning unrelated screens.

Windows logon tasks launch with `--tray`: monitoring and the tray icon start without
showing a splash, dashboard or mini panel. Manual launches retain their normal startup
and mini-mode preference. Restoring a logon launch opens the dashboard directly.

Mini cells keep the percentage as their main reading and show the CPU clock or
used memory/storage beneath it. Reuse `RowSub` through `MiniDetail`; keep values
visible without hovering. Independent columns avoid equal-height empty slots.
Every disk has a compact row with its label, percentage and used capacity; rows
stay in volume order rather than changing with occupancy. The disk group scrolls
when there are many volumes, keeping the mini panel bounded. Its scrollbar owns
drag gestures. GPU details follow the existing GPU selection and visibility rules.
The mini background has no drag-hint tooltip; drag and double-click still work.
The mini component's mappings and validation live in the authoritative JSON.
The mini header uses two transparent clips in a fixed 48 DIP button, rendered at
1.2 scale around the center without changing the readout layout. Hovering the
panel or moving within it while idle plays `Assets/mini-click.webp` once; showing Mini mode also plays it once.
Entering again during playback does not restart either clip. Clicking does not
trigger playback. Dragging beyond the native
mouse threshold moves the panel and plays `Assets/mini-drag.webp` once. Keyboard
activation plays the click clip. Double-click restores the dashboard. Buttons and
disk scrollbars retain their own gestures. There is no periodic timer.
Both clips use a shared 256×256 canvas, matched resting size, horizontal anchor and
foot baseline. Each clip has one fixed transform that fits every motion frame;
first-frame bounds differ by at most one pixel. Playback returns to the common
idle pose. Hiding or closing stops playback; reduced motion and render previews
are static. The splash keeps its original startup asset and layout.
Mini has no LIVE/status
footer; dashboard status presentation keeps its existing owner.

The dashboard owns its vertical scrolling. Its outer page gutter supplies the
single 12 DIP bottom inset; StatBody and its host add no trailing spacing.
Sensor notices add separation only while visible. Auto scrolling remains available
for short windows, battery details and real notices; blank footer space must not
create overflow when the final NET row and page gutter fit.
All five main pages reuse the PageContent style for their 12/4/12/12 DIP page
gutter. Info starts with Refresh and hardware cards, without a duplicate page
heading, accent underline or introductory prose.

Fan custom controls offer temperature curves or constant speed, without a Follow
row or source selector. The fan's hardware determines its temperature: its own GPU,
or CPU for a board fan. Old saved source overrides are ignored. There is no combined
maximum or cross-category substitution when that temperature is missing.
