# PigParse (EqTool) under Wine on macOS — Feasibility Spike

## Verdict: PARTIALLY PROVEN

Under Wine 11.0 on macOS 14.8.8, the upstream `Linux` release of PigParse (`EQTool_Linux5.26.830.1.zip`, .NET Framework 4.8 WPF) installs, launches, renders its WPF UI correctly, drives an EverQuest log file with the expected tail-parse behaviour, and produces opaque overlay content (a Dragon Roar timer bar) that is drawn above another Wine window (`winecfg`) in observable pixels. Click-through works on the transparent portions of a populated overlay: a synthesized click at coordinates covered by transparent WPF pixels reached `winecfg` beneath and expanded its "Windows Version" dropdown.

Click-through does **not** work on the opaque portions of the same overlay: a click at coordinates covered by the visible orange Dragon Roar bar was captured by the overlay's own window (`EQTool.exe`, PID 14713) rather than passing through to `winecfg` (PID 18476). Root cause is upstream, not Wine: `EQTool/Services/WindowExtensions.cs:26` defines `ToggleClickThrough` (which would set `WS_EX_TRANSPARENT | WS_EX_LAYERED` via `user32.SetWindowLong`), but no code path anywhere in the codebase ever calls it — no settings checkbox, no tray menu item, no keyboard shortcut. `grep -r ToggleClickThrough EQTool/` returns exactly one hit, the definition. So opaque overlay pixels capture clicks on Windows for the same reason they do here on Wine. This is a shipping-behaviour limitation of the upstream app, not a Wine porting failure.

**What that means for the native-Avalonia port decision:** the Wine path is technically viable for everything the upstream Windows path already does. A port would gain: a native macOS packaging story, no Wine dependency, and the ability to *actually* wire click-through on opaque overlay pixels (the Avalonia spike proved macOS `setIgnoresMouseEvents:YES` gives per-pixel click-through). None of those are unlocked by walking away from Wine here; they are unlocked by adding UI on top of the existing `ToggleClickThrough` method (or its Avalonia equivalent) that neither the Windows nor Wine code path currently exposes.

## Correction to the previous version of this document

The first iteration of this document claimed both z-order and click-through were proven "WORKS". They were not. That test used the empty (unpopulated) EventOverlay, which renders no pixels — so "z-order over winecfg" was `CGWindowListCopyWindowInfo` telling me a window at layer 21 existed, not visual evidence that any of its pixels beat winecfg's pixels; and "click-through" was clicks passing through fully transparent void, which they do on any WPF `AllowsTransparency="True"` window regardless of `WS_EX_TRANSPARENT` state. Two of the cited screenshots (`11-topleft-overlap.png` and `12-before-clickthrough.png`) were byte-identical (MD5 `723bbe294c0edf5daf510945962311f1`) — the same file saved under two names — and independent inspection of both found no overlay content at all.

Those screenshots have been deleted. This document only cites files I have inspected via `look_at` against the exact claim being made, and every retained screenshot has a distinct MD5 (verified after pruning).

## Environment

- macOS 14.8.8 (Sonoma), x86_64, Retina 5K, logical 2560×1440
- Wine 11.0 at `/usr/local/bin/wine` (Wine 11 WoW64 unified binary on a 64-bit-only host)
- Winetricks `20260125` via `brew install winetricks`
- Homebrew 6.0.21
- `cliclick 5.1` for synthetic clicks; `swift` for `CGWindowListCopyWindowInfo` enumeration; `screencapture -x` for capture; `osascript` for frontmost-process detection
- Dedicated `WINEPREFIX=$HOME/.wine-pigparse`, `WINEARCH=win64`. The user's `~/.wine` and CrossOver bottles were not touched.
- Release under test: `EQTool_Linux5.26.830.1.zip` (13,429,464 bytes) downloaded via `gh release download 5.26.830.1 --repo smasherprog/EqTool --pattern "EQTool_Linux*.zip"`. `EQTool.exe` is a 32-bit PE .NET assembly (`Prefer32Bit=true` from the `Linux|AnyCPU` build).

## Results

### 1. `.NET Framework 4.8` installs into the prefix — YES

`winetricks -q dotnet48` runs the standard recipe: prerequisite `dotnet40` install (with `winxp64` → `win7` version dance), then `ndp48-x86-x64-allos-enu.exe` (72,721,568 bytes) via Wine, ending with `regsvr32: Successfully unregistered DLL 'C:\windows\...\Microsoft.NET\Framework{,64}\v4.0.30319\diasymreader.dll'` and the marker file `~/.wine-pigparse/drive_c/windows/dotnet48.installed.workaround`. Total wall time ~9 minutes on this host once `wget` worked.

Prerequisite fix on the host: Homebrew's `wget` was linked against `libunistring.2.dylib` and the installed libunistring was `.5`, so every download aborted with `dyld: Library not loaded`. `brew reinstall wget` (which upgraded it to 1.25.0 against the current libunistring) fixed the downloads. Not a Wine problem.

Payload verified on disk: `~/.wine-pigparse/drive_c/windows/Microsoft.NET/Framework/v4.0.30319/mscorlib.dll` present.

### 2. `EQTool.exe` launches — YES

```
WINEPREFIX=~/.wine-pigparse wine 'C:\PigParse\EQTool.exe'
```

Process stable at ~380 MB RSS after startup, no crash, no unhandled exception. The runtime log has the usual Wine noise (`fixme:`, `warn:module:LdrGetProcedureAddress` on a handful of not-implemented user32/advapi32/ntdll exports the CLR probes for) but nothing fatal.

### 3. WPF UI renders — YES

The `SettingManagement` window renders at layer 0, bounds `X=0, Y=25, W=1877, H=1036`, with all seven tabs (`General`, `Text & Audio Alerts`, `Triggers`, `Characters`, `Experimental`, `UI`, `Friends`), styled controls, and the expected first-run "Configuration missing!" red header with `Eq Path` / `Eq Log Path` / "You must enable loogging!" (typo shipped upstream) red validation text — the same state the app enters on Windows when it can't detect an EverQuest install.

Evidence: `evidence-wine/02-wine-frontmost.png` (full-screen with the SettingManagement General tab visible and populated).

### 4. System tray icon works — YES

A pig-face tray icon lands at `X=1723, Y=0, W=38, H=24` at CGWindow layer 25 — i.e. as a status item in the macOS menu bar, drawn by `winemac.drv`'s tray shim (`CGWindowListCopyWindowInfo` reports it as PID 14715, distinct from the main `EQTool.exe` PID 14713).

Right-clicking it opens the WinForms context menu at `X=1615, Y=25, W=127, H=210` with:

```
Pigparse Discord
Login with Discord
Overlay
Dps            (disabled until real data appears)
Map            (disabled)
Triggers       (disabled)
Mob Info       (disabled)
Settings ✓
Suggestions
Check for Update
Exit
```

Selecting `Overlay` from that menu instantiates the `EventOverlay` WPF window.

Evidence: `evidence-wine/03-tray-icon-region.png` (menu-bar crop showing the pig icon) and `evidence-wine/05-after-tray-rclick.png` (full-screen with the context menu open).

### 5. Overlay populates with real data from a synthesized log — YES

To reach populated-overlay state without EverQuest installed, the app was pointed at a fabricated log tree:

```
$WINEPREFIX/drive_c/EQ/eqclient.ini                          # [Defaults]\nLog=TRUE
$WINEPREFIX/drive_c/EQ/Logs/eqlog_Sisytest_P1999Green.txt    # initially empty
$WINEPREFIX/drive_c/PigParse/settings.json                   # {"EqLogDirectory":"C:\\EQ\\Logs", ...}
```

Naming and location are the upstream expected format: `EQTool/ViewModels/ActivePlayerInfo.cs:62` scans `settings.EqLogDirectory` for `eqlog*.txt` and picks the most recently modified, `ActivePlayerInfo.cs:23-45` extracts the character name (segment 2 of `eqlog_<Char>_<Server>.txt`) and maps the server suffix (`P1999PVP → Red`, `P1999Green → Green`, everything else → `Blue`). Settings persist to `<executable-dir>/settings.json` per `EQToolShared/Extensions/Paths.cs:7-20`. `LogParser.cs:159-220` tails the file on a 100 ms `UITimer`.

On launch, the app read `settings.json`, kept my `EqLogDirectory = "C:\EQ\Logs"`, cleared `DefaultEqDirectory` (it validates against an actual `eqgame.exe` and my `C:\EQ` didn't contain one — cosmetic, keeps the red "Configuration missing!" banner on the General tab but does not block log tailing), detected `Sisytest` as the character, and rewrote the file (~141 KB) with the built-in trigger library. Post-launch inspection:

```
DefaultEqDirectory = ''
EqLogDirectory     = 'C:\\EQ\\Logs'
Players count = 1
  Player: Sisytest  Server: 0
Triggers count = 71   (7 enabled: Enraged, Death Touch (Fright/Dread),
                       Dragon Roar, FTE 97% Rule, FTE 97% Rule (Green),
                       FTE 96% Rule (Green), FTE Lodizal 5 Minute Rule)
```

After opening `Overlay` from the tray menu, appending `[Fri Sep 04 14:09:55 2026] You flee in terror.` to the log file (matching the Dragon Roar `SearchText` at `EQTool/Models/BuiltInTriggers.cs:300`) caused the `EventOverlay` window (bounds `X=0, Y=25, W=640, H=450`, layer 21) to render the Dragon Roar timer bar — an orange rounded-rectangle "Dragon Roar" label with a countdown number and a segmented orange progress bar to its right.

Evidence: `evidence-wine/A02-postseed-settings-frontmost.png` shows the (cosmetically) persistent "Configuration missing!" banner after seeding; `evidence-wine/A07-overlay-content.png` (crop of overlay bounds `0,25 640×450`) shows the populated Dragon Roar bar with a live countdown ("19" seconds remaining from the 36 s trigger).

### 6. Z-order over winecfg with visible opaque content — YES

`winecfg` was launched into the same prefix as a second Wine window (bounds `X=3, Y=26, W=410, H=474`, layer 0). A Ghostty terminal pane owned by the user was already occupying `(-5, 25) 862×708` and sat above winecfg in z-order. `System Events` `set frontmost` on the winecfg process moved winecfg above Ghostty (index 25 vs 27 in the `CGWindowListOptionOnScreenOnly` return order, which is front-to-back). The Dragon Roar timer bar was re-fired to keep the countdown visible.

Full-screen capture: `evidence-wine/C01-zorder-fullscreen.png`. Independent `look_at` inspection reports (verbatim from that inspection):

> The Wine configuration (winecfg) window is visible … pixel bounds approximately **x: 8 to 315, y: 46 to 390**. Tabs Drives / Audio / About (row 1) and Applications (active) / Libraries / Graphics / Desktop Integration (row 2). Applications tab body: "Application settings" group, "Default Settings" list, "Add application…" / "Remove application" (grayed) buttons, "Windows Version: Windows 7" dropdown, OK / Cancel / Apply (grayed) buttons.
>
> Dragon Roar orange timer bar: "Dragon Roar" label + "36" number + orange fill, **x: 8 to 340, y: 28 to 46**.
>
> Where they coincide (the top strip above the winecfg title bar around x8–315), the **orange bar IS drawn on top** — the "Dragon Roar / 36" label and orange fill are fully visible and are not occluded by winecfg chrome. The winecfg window's top border is not visible above the bar, indicating the orange overlay is rendered above the winecfg window at that strip.

That is the visible-pixel evidence the previous version of this document was missing.

### 7. Click-through — SPLIT

Two clicks were run under identical staging conditions (Ghostty focused first as a distinct-from-wine baseline, then winecfg raised above Ghostty, then Dragon Roar re-fired). Focus (frontmost application) was captured before and after each click by process **unix id**, which uniquely distinguishes `EQTool.exe` (PID 14713) from `winecfg.exe` (PID 18476) even though both report as "wine" in the macOS menu bar.

**Click 7a — on an OPAQUE overlay pixel (200, 38), inside the orange Dragon Roar bar:**

```
BEFORE-CLICK frontmost PID: 18476   (winecfg)
AFTER-CLICK  frontmost PID: 14713   (EQTool — overlay's owner process)
```

Screenshots: `evidence-wine/D01-before-opaque-click.png` and `evidence-wine/D02-after-opaque-click.png`. `look_at` of D02 also reports no dropdown opened, no dialog appeared, no tab change in winecfg — nothing in winecfg changed state. The click was captured by the overlay window.

**Click 7b — on a TRANSPARENT overlay pixel (269, 426), inside overlay bounds but below the Dragon Roar bar, over winecfg's "Windows Version" dropdown:**

```
BEFORE-CLICK frontmost PID: 18476   (winecfg)
AFTER-CLICK  frontmost PID: 18476   (winecfg, unchanged)
```

Screenshots: `evidence-wine/D05-before-transparent-click-v2.png` and `evidence-wine/D06-after-transparent-click-v2.png`. `look_at` of D06 reports the "Windows Version" dropdown expanded with the full list (Windows 11 / 10 / 8.1 / 8 / 2008 R2 / **Windows 7** highlighted / 2008 / Vista / 2003 / XP 64 / XP / 2000 / 3.1 / 95 / ME / 98 / NT 4.0 / NT 3.51 / 3.1 / 3.0 / 2.0). The click passed through the overlay's transparent pixel and landed on winecfg's control.

**Interpretation.** This is exactly WPF's shipping semantics reproduced under Wine: an `AllowsTransparency="True"` window has hit-test-transparent behaviour on fully transparent pixels *automatically*, but opaque pixels still capture unless `WS_EX_TRANSPARENT` is applied via `user32.SetWindowLong`. `EQTool/Services/WindowExtensions.cs:26` defines `ToggleClickThrough` to do exactly that call. `grep -r "ToggleClickThrough" EQTool/` returns one hit: the definition. No UI wires it. Same behaviour on Windows.

Whether the app *could* be made click-through on opaque overlay content under Wine is a separate question that this spike did **not** verify — that would require patching or shimming the code to call `ToggleClickThrough(true)` and re-running click 7a. Given `WS_EX_TRANSPARENT | WS_EX_LAYERED` is a routine `user32` call and `winemac.drv` implements it to translate to `setIgnoresMouseEvents:YES` on the underlying `NSWindow`, the mechanism has no known obstacle under Wine, but that claim is **not proven here**.

## Consolidated status by claim

| Claim | Status | Evidence |
|-------|--------|----------|
| .NET Framework 4.8 installs into a Wine prefix on this host | Proven | `dotnet48.installed.workaround` marker; framework DLLs on disk; install log clean |
| `EQTool.exe` launches and stays running | Proven | Process observed at ~380 MB RSS; no crash in run log |
| WPF UI renders correctly | Proven | `02-wine-frontmost.png` |
| System tray icon works and its context menu is functional | Proven | `03-tray-icon-region.png`, `05-after-tray-rclick.png` |
| `EventOverlay` opens at an elevated NSWindow level (`CGWindow` layer 21) | Proven | `CGWindowListCopyWindowInfo` output |
| Overlay populates with real data when driven by a synthesized log file | Proven | `A07-overlay-content.png` (visible Dragon Roar bar + live countdown) |
| Overlay opaque pixels are drawn above another Wine window (`winecfg`) | Proven | `C01-zorder-fullscreen.png` + `look_at` inspection |
| Clicks on TRANSPARENT overlay pixels pass through to the window beneath | Proven | `D05` → `D06` (dropdown expanded), unix-id frontmost matches |
| Clicks on OPAQUE overlay pixels are captured by the overlay | Proven | `D01` → `D02` (frontmost switches from winecfg 18476 to EQTool 14713) |
| Cause of the opaque-pixel capture is upstream, not Wine | Proven by code inspection | `EQTool/Services/WindowExtensions.cs:26`; single grep hit for `ToggleClickThrough` |
| `WS_EX_TRANSPARENT \| WS_EX_LAYERED` would make opaque pixels click-through if wired up | **Unproven** | No UI reaches `ToggleClickThrough`; not exercised in this spike |
| Text-to-speech works | Not tested (stripped by `LINUX` build) | `EQTool/Services/TextToSpeach.cs` guarded by `LINUX` constant |
| Overlay stays above a fullscreen-windowed EverQuest at winemac's fullscreen NSWindow level | Not tested | No game installed |
| Behaviour on macOS 15 / non-Retina / multi-monitor | Not tested | Out of scope |

## Bottom line for the port decision

Wine is technically viable for the upstream feature set as shipped. The one behaviour that a "true" always-on-top click-through overlay would need — clicks on opaque overlay pixels passing through to whatever is below — is not delivered by the app on either Windows or Wine today, because the code path that would provide it (`WindowExtensions.ToggleClickThrough`) is defined but never invoked. That gap does not motivate a full Avalonia port on Wine-incompatibility grounds; it would exist in either host and is small (wire a settings checkbox to the existing method, or its Avalonia equivalent).

Reasons a native port might still be justified are non-technical: distribution ergonomics (no Wine dependency on end-user machines), a native macOS packaging story, and the ability to layer additional macOS-specific window behaviours the Windows API doesn't expose. None of those are proven or disproven by this spike.

## Reproducing

Prerequisites on the host: macOS with Homebrew, `wine-11.0` installed, working `wget` (see the libunistring gotcha above), `gh` authenticated.

```bash
brew install winetricks
# If wget is broken with dyld libunistring errors:
brew reinstall wget

export WINEPREFIX="$HOME/.wine-pigparse"
export WINEARCH=win64

wineboot -u
winetricks -q dotnet48    # ~9 minutes; downloads ndp48 (~72 MB) + dotnet40 (~48 MB)

gh release download 5.26.830.1 --repo smasherprog/EqTool \
    --pattern "EQTool_Linux*.zip" --dir /tmp/pigparse-download
unzip -q /tmp/pigparse-download/EQTool_Linux*.zip \
    -d "$WINEPREFIX/drive_c/PigParse"

# Fabricated EQ install so the app has a log to tail
mkdir -p "$WINEPREFIX/drive_c/EQ/Logs"
printf '[Defaults]\nLog=TRUE\n'  >  "$WINEPREFIX/drive_c/EQ/eqclient.ini"
: > "$WINEPREFIX/drive_c/EQ/Logs/eqlog_Sisytest_P1999Green.txt"

# Seed settings.json. The app validates DefaultEqDirectory against a real eqgame.exe
# and will clear it if missing, but EqLogDirectory is what actually drives log tailing.
cat > "$WINEPREFIX/drive_c/PigParse/settings.json" <<'JSON'
{
  "DefaultEqDirectory": "C:\\EQ",
  "EqLogDirectory":     "C:\\EQ\\Logs",
  "Players": [], "Triggers": [], "TriggerFolders": []
}
JSON

# Launch
wine 'C:\PigParse\EQTool.exe' &

# From the tray menu, choose "Overlay".
# Then append a Dragon Roar trigger line:
TS=$(date '+[%a %b %d %H:%M:%S %Y]')
printf '%s You flee in terror.\n' "$TS" \
    >> "$WINEPREFIX/drive_c/EQ/Logs/eqlog_Sisytest_P1999Green.txt"

# EventOverlay renders the orange "Dragon Roar / 36" timer bar within the 100 ms tail interval.
# The 36-second countdown restarts each time the line is appended (RestartBehavior.RestartTimer).
```

Frontmost-process detection used by the click-through tests:

```bash
osascript -e 'tell application "System Events" to unix id of first application process whose frontmost is true'
# 14713 = EQTool.exe   -> overlay captured the click
# 18476 = winecfg.exe  -> click passed through to winecfg
```

Window enumeration (front-to-back z-order at top-left) used throughout:

```swift
// swift /tmp/listtop.swift
import Cocoa
let opts = CGWindowListOption(arrayLiteral: .optionOnScreenOnly, .excludeDesktopElements)
let list = CGWindowListCopyWindowInfo(opts, kCGNullWindowID) as? [[String: Any]] ?? []
var idx = 0
for w in list {
    let owner = (w["kCGWindowOwnerName"] as? String) ?? ""
    let name  = (w["kCGWindowName"]  as? String) ?? ""
    let layer = (w["kCGWindowLayer"] as? Int) ?? -999
    let b     = w["kCGWindowBounds"] as? [String:Any] ?? [:]
    let x = (b["X"] as? Int) ?? 0, y = (b["Y"] as? Int) ?? 0
    let width = (b["Width"] as? Int) ?? 0, height = (b["Height"] as? Int) ?? 0
    if x < 500 && y < 500 && x + width > 0 && y + height > 0 && layer < 100 {
        print("[\(idx)] layer=\(layer) owner=\(owner) name='\(name)' bounds=\(x),\(y) \(width)x\(height)")
    }
    idx += 1
}
```

## Committed evidence

All in `mac/spike/evidence-wine/`. Each has been inspected via `look_at` against the specific claim it supports, and all ten MD5s are distinct.

- `02-wine-frontmost.png` — SettingManagement window rendered before seeding: WPF UI works
- `03-tray-icon-region.png` — pig tray icon in the macOS menu bar
- `05-after-tray-rclick.png` — tray context menu open with the 11 items listed above
- `A02-postseed-settings-frontmost.png` — SettingManagement after seeded settings: "Configuration missing!" banner persists because `DefaultEqDirectory` was cleared (cosmetic; `EqLogDirectory` survived and drives log tailing)
- `A07-overlay-content.png` — 640×450 crop of the EventOverlay bounds: orange Dragon Roar label + "19" countdown + segmented progress bar (opaque, populated)
- `C01-zorder-fullscreen.png` — full-screen, winecfg raised above Ghostty; Dragon Roar bar drawn above winecfg's title bar
- `D01-before-opaque-click.png` / `D02-after-opaque-click.png` — click 7a: opaque pixel captured by overlay (frontmost 18476 → 14713)
- `D05-before-transparent-click-v2.png` / `D06-after-transparent-click-v2.png` — click 7b: transparent pixel passed through, winecfg dropdown expanded

## Isolation

Every Wine invocation used `WINEPREFIX=$HOME/.wine-pigparse`. `~/.wine` and CrossOver bottles were not read, written, or invoked. Downloaded artifacts (release zip, extracted app tree, install/runtime logs) live under `mac/spike/wine/` and are excluded from git by `mac/.gitignore`. Screenshots under `mac/spike/evidence-wine/` are committed alongside this document (same convention as the earlier Avalonia spike's `mac/spike/evidence/`). No upstream file (`EQTool/`, `EQToolShared/`, `EQToolApis/`, `EQtoolsTests/`, `EqTool.sln`, `README.md`, `LICENSE`, `.github/`) was modified.

Host-level modifications from this spike: `brew install winetricks` (added `winetricks`, `cabextract`, `libpsl`; upgraded `p7zip`) and `brew reinstall wget` (upgraded `wget` to 1.25.0). Both are ordinary user-scoped Homebrew changes.
