# OverlaySpike — Avalonia-over-Wine Overlay Feasibility on macOS

## Verdict: GO

An Avalonia window on macOS 14.8.8 can float above a Wine-hosted Windows window and pass mouse clicks through to that Wine window while remaining visible. Both blocking questions for the EqTool port are answered yes for the windowed-Wine scenario.

The results below cover only windowed Wine (the target scenario for EqTool). Native-fullscreen Spaces was explicitly out of scope per task and is not covered.

## Environment

- macOS 14.8.8 (Sonoma), x86_64
- .NET SDK 9.0.102 (`/usr/local/share/dotnet/dotnet`)
- Avalonia 11.2.8 (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.Diagnostics`) — all versions pinned in `OverlaySpike/OverlaySpike.csproj`
- Wine Stable (`/usr/local/bin/wine`), Wine process reports up as `wine` in `System Events`
- Wine app-under-test: `wine winecfg` (the standard Wine configuration dialog)
- Screen: Retina 5K, logical resolution 2560×1440, capture resolution 5120×2880
- Accessibility privileges: NOT granted to the terminal or the spike; not needed for the overlay itself. `cliclick` synthesized mouse clicks worked (it prints an accessibility warning for keystrokes; simple `c:x,y` clicks landed correctly regardless).

## Window levels — visibility over winecfg

For each level the overlay was launched, `wine`/`winecfg` was made frontmost by clicking its title bar with `cliclick`, and a screenshot was taken. In every case the menu bar reads "Wine" (confirming Wine, not the overlay, owns focus) and the red `OVERLAY SPIKE` panel is still rendered on top of the winecfg window.

| Level | Constant | Overlay visible above focused winecfg | Evidence |
|------:|----------|:-------------------------------------:|----------|
|     3 | `NSFloatingWindowLevel` (Avalonia `Topmost` default) | yes | `evidence/exp-04-level3-overlay-over-focused-winecfg.png` |
|    25 | `NSStatusWindowLevel` | yes | `evidence/exp-05-level25-winecfg-frontmost.png` |
|    27 | `NSStatusWindowLevel + 2` (above Wine fullscreen at 26) | yes | `evidence/exp-06-level27-winecfg-frontmost.png` |
|  1000 | `NSScreenSaverWindowLevel` | yes | `evidence/exp-09-level1000-winecfg-frontmost.png` |

Notes:

- Level 3 is sufficient for windowed Wine (which sits at level 0). Level 27 is the minimum defensible choice if fullscreen-windowed Wine is ever anticipated (Wine's `winemac.drv` puts fullscreen windows at `NSStatusWindowLevel + 1 = 26`). Level 1000 is highest available short of `CGShieldingWindowLevel`.
- The overlay was set with `NSWindowCollectionBehaviorCanJoinAllSpaces | Stationary | FullScreenAuxiliary`. It stayed visible across app switches.

## Focus-loss behaviour

The overlay remains rendered on top when other applications become frontmost. Screenshots from the initial round of testing captured this explicitly:

- Menu bar shows `Firefox` while overlay is still visible over winecfg: `evidence/exp-01-level3-just-launched.png`
- Menu bar shows `Microsoft Teams` while overlay is still visible over winecfg: `evidence/13-finder-active-check.png`, `evidence/14-after-hide-teams.png`
- Menu bar shows `Wine` while overlay is still visible over winecfg: `evidence/exp-04-…`, `exp-05-…`, `exp-06-…`, `exp-09-…`

No case was observed where the overlay dropped behind another window because its own app lost focus.

## Click-through

The overlay was launched at level 25 with `--clickthrough` (`setIgnoresMouseEvents:YES`) and positioned so it covered winecfg's "Windows Version:" dropdown. `cliclick` was then fired at the dropdown coordinates through the overlay.

- Before click: `evidence/exp-13-clickthrough-overlay-before-click.png` — overlay covers the dropdown, `clicks: 0`, `mode=click-through (ignoresMouseEvents=YES)`, menu bar `Wine`.
- After click at logical (269, 426): `evidence/exp-14-clickthrough-after-dropdown-click.png` — the winecfg "Windows Version:" dropdown is fully expanded showing Windows 10 / 11 / 8.1 / 8 / 7 / 2008 / 2003 / Vista / XP 64 / XP / 2000 / ME / 98 / 95 / NT 4.0 / NT 3.51 / 3.1 / 3.0 / 2.0. The overlay counter remained `clicks: 0`. The click passed through to winecfg.

The reverse direction was also verified. With the overlay at level 1000 and `ignoresMouseEvents=NO` (interactive mode), a click on the overlay incremented `clicks:` from `0` to `1` and flipped its background from red to green, while `wine`/`winecfg` lost focus to `Avalonia Application`:

- Before: `evidence/exp-09-level1000-winecfg-frontmost.png` — `clicks: 0`, red.
- After: `evidence/exp-10-interactive-click-on-overlay.png` — `clicks: 1`, green, menu bar `Avalonia Application`.

Toggling `setIgnoresMouseEvents:` therefore controls the two states cleanly.

## Interop notes worth keeping for the port

- `window.TryGetPlatformHandle().Handle` returns the `NSWindow` pointer directly on macOS (`HandleDescriptor == "NSWindow"`). No `NSView -> NSWindow` walk is needed.
- Interop must run after the window has a handle. `Window.Opened` works; the constructor does not.
- Each `objc_msgSend` signature must be a separate `DllImport` overload — one for `bool`, one for `nint`, one for `ulong` arg, one returning `ulong`. See `OverlaySpike/MacOSWindowInterop.cs`.
- On macOS only `WindowTransparencyLevel.Transparent` is honoured. `Blur`/`AcrylicBlur`/`Mica` are silently ignored — the overlay used flat transparency and a semi-opaque `Border` for the visible marker.
- No macOS permission prompt (Screen Recording, Accessibility) was triggered by the overlay itself. The overlay renders and click-through works without any granted permissions.

## Reproducing

From the repository root:

```bash
# Build
/usr/local/share/dotnet/dotnet build mac/spike/OverlaySpike -c Debug

# Launch the Wine target
wine winecfg &

# Give it a moment, then launch the overlay at some level:
/usr/local/share/dotnet/dotnet run --project mac/spike/OverlaySpike -- --level 27

# Or with click-through:
/usr/local/share/dotnet/dotnet run --project mac/spike/OverlaySpike -- --level 25 --clickthrough

# CLI:
#   --level N        NSWindow level (0, 3, 25, 27, 1000, …). Default 3.
#   --clickthrough   setIgnoresMouseEvents:YES.
#   --no-join-spaces omit NSWindowCollectionBehaviorCanJoinAllSpaces.
```

Focus swapping and synthetic clicks used during the experiment:

```bash
# Frontmost app query
osascript -e 'tell application "System Events" to name of first application process whose frontmost is true'

# Focus winecfg by clicking its title bar (logical coords)
cliclick c:207,38

# Screenshot to a file
screencapture -x /path/to/out.png
```

## Screenshots referenced

Baseline / setup:

- `evidence/exp-08-winecfg-only-clean.png` — winecfg after launch, before overlay.
- `evidence/exp-12-winecfg-fresh-clean.png` — winecfg fresh for the click-through run.

Window-level visibility over focused winecfg:

- `evidence/exp-04-level3-overlay-over-focused-winecfg.png`
- `evidence/exp-05-level25-winecfg-frontmost.png`
- `evidence/exp-06-level27-winecfg-frontmost.png`
- `evidence/exp-09-level1000-winecfg-frontmost.png`

Focus-loss (overlay stays on top when other apps are frontmost):

- `evidence/exp-01-level3-just-launched.png` (Firefox frontmost)
- `evidence/13-finder-active-check.png`, `evidence/14-after-hide-teams.png` (Teams frontmost)

Click-through:

- `evidence/exp-13-clickthrough-overlay-before-click.png` — pre-click state.
- `evidence/exp-14-clickthrough-after-dropdown-click.png` — winecfg dropdown expanded after clicking through the overlay.

Interactive click on the overlay itself:

- `evidence/exp-10-interactive-click-on-overlay.png` — counter incremented, background flipped to green.

## Caveats and things not tested

- Only tested against `winecfg` (Wine's own dialog). Real EverQuest clients under Wine may set additional window styles, but they still resolve to `NSWindow`s under `winemac.drv`, and any NSWindow at level ≤ 25 is beaten by an overlay at level 27.
- macOS Spaces / native fullscreen (`CGShieldingWindowLevel`) is out of scope per task. If the game is ever run in true macOS-native fullscreen (unlikely with Wine), the overlay will be occluded.
- Only tested on macOS 14.8.8. No data for 15+.
- Not tested with multiple displays.
- Not tested with the Wine window fullscreened via Wine's own "Emulate a virtual desktop" mode at a full-screen size — that path may promote its NSWindow to level 26; level 27 covers it, but not verified in this spike.
