# PigParse Mac build under Wine — three-question feasibility test

## Verdicts

| Question | Verdict | Confidence |
|----------|---------|------------|
| Q1: settings window still clickable, and the new Click-through checkbox is present and checked | **PASS** | High |
| Q2: clicks pass through OPAQUE overlay pixels | **DOES NOT WORK** | High |
| Q3: updater is genuinely inert | **PASS** | High |

The gating question (Q2) came back negative. The Mac-flavoured build reaches `ToggleClickThrough(true)` on `EventOverlay` at `SourceInitialized`, but under Wine 11.0 on macOS 14.8.8 that translates to no observable per-pixel click-through: opaque overlay pixels still capture mouse clicks, exactly as the stock build does. The transparent-pixel baseline still passes through, as before.

## Build and setup under test

- macOS 14.8.8, Retina 5K (logical 2560×1440, screen captures are 5120×2880).
- Wine 11.0 at `/usr/local/bin/wine`, prefix `WINEPREFIX=$HOME/.wine-pigparse` (already had `dotnet48` installed for the earlier stock spike).
- Mac build source: `/tmp/eqtool-mac-artifact/` (downloaded from GitHub Actions run 33902718663, the `Mac` config artifact).
  - `EQTool.exe` MD5: `ef8501bcd7539862f9eee52278d0df23` (stock build MD5 is `248e5ee432d2e3ffa2110f63caf16d21` — different binary).
- Installed to `$WINEPREFIX/drive_c/PigParseMac/` via `cp -R /tmp/eqtool-mac-artifact/. "$WINEPREFIX/drive_c/PigParseMac/"`. The stock install at `drive_c/PigParse/` was left untouched.
- Fabricated EQ tree under `$WINEPREFIX/drive_c/EQ/`:
  - `eqclient.ini` containing `[Defaults]\nLog=TRUE\n`
  - `Logs/eqlog_Sisytest_P1999Green.txt` (drives the built-in Dragon Roar trigger via `You flee in terror.` lines with a current timestamp)
  - `license.txt` containing the string `Project 1999` — required so `FindEq.IsValidEqFolder` returns true, which is what makes `NotMissingConfiguration` true, which is what makes the "Overlay Windows" group visible in the General tab
- `settings.json` in `drive_c/PigParseMac/` was seeded with `DefaultEqDirectory=C:\EQ`, `EqLogDirectory=C:\EQ\Logs`. `OverlayClickThrough` defaults to `true` in the Mac build's `EQToolSettings` and was verified `true` in the on-disk settings file at every stage:
  ```
  $ grep OverlayClickThrough $WINEPREFIX/drive_c/PigParseMac/settings.json
    "OverlayClickThrough": true,
  ```
- App launched with working directory set to `drive_c/PigParseMac/` so `Errors.txt` would land there. It never did.

Version string on the tray version item reads `Mac1.0.0.0`, confirming the `MACOS` build path was taken.

Every wine invocation used `WINEPREFIX=$HOME/.wine-pigparse`. `~/.wine` and CrossOver bottles were not touched. The stock install at `drive_c/PigParse/` was not read, written, or launched during this test.

## Q1 — settings window and Click-through checkbox

### Regression check (settings clickability)

`SettingManagement` derives from `BaseSaveStateWindow`. If the base class had applied click-through unconditionally the settings window itself would become uninteractable — you would not be able to turn the feature back off. The guard is `SupportsClickThrough`, `false` by default, overridden `true` only in `EventOverlay` and `SpellWindow`.

Test procedure:

1. Launch the Mac build with the settings window open and every overlay window forced closed (via `SettingsWindowState.Closed=false`, all other `*WindowState.Closed=true`) so the tab strip along the top of the settings window is not covered.
2. Take a pre-click screenshot with the General tab active (default).
3. `cliclick c:136,59` — logical-point centre of the "Text & Audio Alerts" tab header, derived from the pre-click screenshot inspection.
4. Take a post-click screenshot and inspect which tab is now shown.

Result: after the click, the visible tab body switched from Configuration/Character/Display/Audio (General tab) to Installed Voices/Text Alert/Audio Alert/Chain Formats/Class Filters (Text & Audio Alerts tab). The settings window responds to clicks — no critical regression.

Evidence:
- `evidence-macbuild/Q1c-preclick-general.png` — pre-click, General tab active
- `evidence-macbuild/Q1d-after-tab-click.png` — post-click, Text & Audio Alerts tab active

### Click-through checkbox present and checked

The General tab is wrapped in `<StackPanel Visibility="{Binding NotMissingConfiguration ...}">`, so the "Overlay Windows" group only appears once `HasEqPath && IsLoggingEnabled` is true. With the fake `license.txt` + `Log=TRUE` in place, both conditions hold, and the group renders.

Independent `look_at` inspection of `evidence-macbuild/Q1b-configured.png` (settings open with configuration satisfied):

> Overlay Windows group contents (top-left area, roughly x 5–500, y 375–500 in the 5120×2880 image):
> — Header "Overlay Windows"
> — Column headers: "On top" and "Opacity"
> — Rows (label / On top checkbox — all unchecked except Click-through):
>   Damage — unchecked
>   Triggers — unchecked
>   Mob — unchecked
>   Map — unchecked
>   **Click-through — CHECKED ✓**

The Click-through row is bound to `OverlayClickThrough` per `EQTool/UI/SettingsComponents/SettingsGeneral.xaml:178-179`, and the check state matches the `true` default seeded by `EQToolSettings.cs:159` under `#if MACOS`.

Evidence: `evidence-macbuild/Q1b-configured.png`.

Q1 verdict: **PASS**. Settings window is clickable, Click-through checkbox is present in the correct group, checkbox state is CHECKED on first launch consistent with the Mac default.

## Q2 — clicks through OPAQUE overlay pixels (the gating question)

Verdict: **DOES NOT WORK.** Same observable behaviour as the stock build documented in `WINE-FINDINGS.md` section 7.

### Setup for the click tests

- `EventOverlay` open (via `OverlayWindowState.Closed=false` in settings, verified as CGWindow at layer 21, bounds `0,25 640x450`).
- `winecfg` launched into the same prefix as a second Wine top-level: `WINEPREFIX=$HOME/.wine-pigparse nohup wine winecfg &`. It appeared at layer 0, bounds `3,26 410x474`, PID 14613.
- The Mac-build EQTool process (owning `EventOverlay`) at PID 14179.
- Before each click, `winecfg` was raised to frontmost:
  ```
  osascript -e 'tell application "System Events" to set frontmost of first application process whose unix id is 14613 to true'
  ```
- Before each click, a fresh Dragon Roar trigger was fired:
  ```
  TS=$(date '+[%a %b %d %H:%M:%S %Y]')
  printf '%s You flee in terror.\n' "$TS" \
      >> "$WINEPREFIX/drive_c/EQ/Logs/eqlog_Sisytest_P1999Green.txt"
  ```
  which produces a 36-second opaque orange countdown bar in the overlay, matching `BuiltInTriggers.cs:300`.
- Frontmost was captured immediately before and immediately after each click:
  ```
  osascript -e 'tell application "System Events" to unix id of first application process whose frontmost is true'
  # 14613 → winecfg had focus (click did not switch focus, or click reached winecfg)
  # 14179 → EQTool had focus (click was captured by the overlay window)
  ```

`look_at` on the pre-click screenshots confirmed the opaque orange bar bounds inside the overlay: pixel (~15, 66) to (~1257, 111) in the 5120×2880 image, i.e. logical (~8, 33) to (~628, 55). All opaque-pixel clicks were at `cliclick c:100,45` — logical point squarely inside that band.

### Three tests

**Test D (opaque click, first attempt):**

```
BEFORE frontmost: 14613  (winecfg)
Click:            cliclick c:100,45   (opaque orange bar)
AFTER frontmost:  14179  (EQTool overlay)
```

Screenshots: `evidence-macbuild/Q2d-before-opaque-click.png` (bar visible, "35" countdown, winecfg Applications tab active beneath) and `evidence-macbuild/Q2d-after-opaque-click.png` (bar still visible at "34", winecfg's Applications tab still active, no dropdown expanded, but frontmost application is now the EQTool overlay process). No visible dropdown expansion or state change in winecfg — winecfg received nothing. The overlay swallowed the click.

**Test E (opaque click, repeat for determinism):**

```
BEFORE frontmost: 14613  (winecfg)
Click:            cliclick c:100,45   (opaque orange bar)
AFTER frontmost:  14179  (EQTool overlay)
```

Screenshots: `evidence-macbuild/Q2e-before-opaque-repeat.png` and `evidence-macbuild/Q2e-after-opaque-repeat.png`. Same outcome — click captured.

**Test F (transparent-pixel baseline, calibration):**

Same setup, click at `cliclick c:150,415` — deep inside the overlay's bounds but well below the bar (logical y=415 inside the 640×450 overlay, over winecfg's lower body area which contains the "Windows Version" combobox).

```
BEFORE frontmost: 14613  (winecfg)
Click:            cliclick c:150,415  (transparent overlay pixel, over winecfg)
AFTER frontmost:  14613  (winecfg, unchanged)
```

Screenshots: `evidence-macbuild/Q2f-before-transparent.png` and `evidence-macbuild/Q2f-after-transparent.png`. Frontmost stays winecfg — the click passed through the transparent portion of the WPF overlay and landed on winecfg beneath, consistent with WPF's automatic hit-test-transparency on fully transparent `AllowsTransparency="True"` pixels. This confirms the click plumbing itself is intact and cliclick coordinates are landing where intended; the failure in D/E is specifically about opaque pixels.

An earlier attempt (`Q2c-after-opaque-click.png`) reported `AFTER frontmost: 14613` (unchanged). That result was not reproducible: the AFTER screenshot for that run was taken more than a minute after the trigger fired, by which time the 36-second Dragon Roar countdown had elapsed and the bar was no longer drawn — so the recorded frontmost may reflect a click that actually landed after the bar had already disappeared, not click-through on an opaque pixel. Tests D and E were run with a screencapture immediately after the `cliclick` invocation, before any timer could expire, and both showed frontmost switching to EQTool. I am treating D/E as the load-bearing evidence and calling Q2c out as an artifact rather than massaging it into a pass.

### Comparison to the stock-build baseline

`WINE-FINDINGS.md` section 7 documented, on the stock `Linux` build with `OverlayClickThrough` unwired:

| Click | Stock build (`WINE-FINDINGS.md`) | Mac build (this test) |
|-------|----------------------------------|-----------------------|
| Opaque overlay pixel (bar) | frontmost 18476 → 14713 (captured by EQTool) | frontmost 14613 → 14179 (captured by EQTool) |
| Transparent overlay pixel | frontmost 18476 → 18476 (passed through) | frontmost 14613 → 14613 (passed through) |

**Behaviour is identical.** Wiring `ToggleClickThrough(true)` in on the Mac build produced no observable change in the click-through behaviour on opaque pixels under Wine 11.0 on macOS. `WS_EX_TRANSPARENT | WS_EX_LAYERED` set via `user32.SetWindowLong` — the call `ToggleClickThrough` performs — does not translate into `NSWindow setIgnoresMouseEvents:YES` for a WPF `AllowsTransparency="True"` window under this Wine version on this host, at least not for opaque WPF pixels.

The unproven claim from `WINE-FINDINGS.md` line 141 ("the mechanism has no known obstacle under Wine, but that claim is **not proven here**") is now tested and refuted for the actual code path the Mac build takes. The mechanism does have an obstacle in practice; where exactly the obstacle sits (winemac.drv, Wine's WPF-composited window handling, or the interaction with `WS_EX_LAYERED` on an already-transparent WPF window) was not investigated further as part of this test.

Guard against a false negative — confirmed:
- `settings.json` on disk contained `"OverlayClickThrough": true` before the test and continued to contain it throughout.
- `MACOS` build path was taken: tray version item reads `Mac1.0.0.0`.
- `EventOverlay` derives from `BaseSaveStateWindow` and overrides `SupportsClickThrough => true` (`EQTool/UI/EventOverlay.xaml.cs:46`); `BaseSaveStateWindow.OnSourceInitializedApplyClickThrough` calls `this.ToggleClickThrough(settings.OverlayClickThrough)` exactly when `SupportsClickThrough` is true.
- Transparent-pixel baseline still works, so click coordinates were reaching the intended location.

Nothing in the setting or the reachability of `ToggleClickThrough(true)` is at fault. The Wine layer is not delivering what the Windows API contract implies.

Q2 verdict: **DOES NOT WORK.** The feature the Mac build wired in for the sake of Wine users does not, under Wine, deliver the behaviour it delivers on Windows. Anyone considering shipping this build to Wine users because of the click-through feature should recalibrate on that finding.

## Q3 — updater inertness

Mac build had all three unconditional-update entry points hard-guarded (`UpdateService.ApplyUpdate`, both `CheckForUpdates` overloads, and `UpdateRunner.UpdateTimer_Elapsed`), each with an early return under `#if MACOS`.

The app ran continuously from `19:02:12` to at least `19:10:27` (`ps -o etime` reported `08:15`), well past the 3-minute mark and past the 60-second `UpdateRunner` poll interval. During that window:

- No "update available" dialog appeared.
- No "downloading update" or "restarting to apply update" dialog appeared.
- No error dialog appeared.
- `$WINEPREFIX/drive_c/PigParseMac/EQTool.exe` MD5 stayed `ef8501bcd7539862f9eee52278d0df23` — byte-identical to what was copied in. It has not been overwritten by an update payload.
- No `NewVersion/` directory appeared under `drive_c/PigParseMac/` (or anywhere else in the install directory):
  ```
  $ ls -la $WINEPREFIX/drive_c/PigParseMac/ | grep -iE 'newversion|update'
  (no output)
  ```
- No `Errors.txt` was written to `drive_c/PigParseMac/`.

Then, as a positive-provocation check, the tray menu's "Check for Update" item was invoked:

```
# Tray icon at bounds (1571,0) size 38x24 — right-click to open the context menu.
cliclick rc:1590,12
# Menu opened at bounds (1463,25) size 127x210. "Check for Update" at logical y=190.
cliclick c:1526,190
```

Screenshots: `evidence-macbuild/Q3a-tray-menu.png` (menu open with 12 items: Pigparse Discord / Login with Discord / Overlay ✓ / Dps / Map / Triggers / Mob Info / Settings / Suggestions / Check for Update / **Mac1.0.0.0** / Exit) and `evidence-macbuild/Q3b-post-check-for-update.png` (3 seconds after clicking "Check for Update" — no dialog, no notification, no popup; app process still alive; MD5 still `ef8501bcd7539862f9eee52278d0df23`; still no `NewVersion/`).

The version-label item explicitly reading `Mac1.0.0.0` is additional confirmation the Mac-specific build path was taken.

Q3 verdict: **PASS.** Both the automatic timer path and the user-triggered "Check for Update" path are inert.

## Evidence

All screenshots live in `mac/evidence-macbuild/`. Each was captured with `screencapture -x`, downscaled with `sips -Z 1200`, and independently inspected via `look_at` against the specific claim it supports. MD5s of the 15 pre-scale captures are distinct.

- `Q1a-initial-settings.png` — first launch, no `license.txt`, no `DefaultEqDirectory`; General tab shows "Configuration missing!" and Overlay Windows group is hidden (as expected).
- `Q1b-configured.png` — configuration satisfied; Overlay Windows group visible; Click-through checkbox CHECKED.
- `Q1c-preclick-general.png` — settings-only view (other overlays forced closed), General tab active before the tab click.
- `Q1d-after-tab-click.png` — after `cliclick c:136,59`, Text & Audio Alerts tab active. Settings window is clickable.
- `Q2a-overlay-bar.png` — opaque Dragon Roar bar visible in the EventOverlay right after seeding.
- `Q2d-before-opaque-click.png` / `Q2d-after-opaque-click.png` — test D, opaque click, frontmost 14613 → 14179.
- `Q2e-before-opaque-repeat.png` / `Q2e-after-opaque-repeat.png` — test E, opaque click repeat, frontmost 14613 → 14179.
- `Q2f-before-transparent.png` / `Q2f-after-transparent.png` — test F, transparent baseline, frontmost 14613 → 14613 (passed through as expected).
- `Q2b-before-opaque-click.png` / `Q2c-after-opaque-click.png` — the first opaque attempt where the countdown had expired before the AFTER capture; kept for completeness and called out above as an artifact, not evidence.
- `Q3a-tray-menu.png` — tray context menu with the 12 items including the `Mac1.0.0.0` version label.
- `Q3b-post-check-for-update.png` — 3 seconds after invoking "Check for Update"; no dialog appeared.

## Things I could not determine, or that surprised me

- I could not confirm from the runtime whether `WS_EX_TRANSPARENT` was actually set on the overlay's HWND at the moment of the click, only that the code path that sets it is reachable and was reached (`SupportsClickThrough=true` on `EventOverlay`, `SourceInitialized` fires before user interaction, `OverlayClickThrough=true` in settings, MACOS build is confirmed). To close that gap you would need to run a WinAPI probe inside the same process (a debugger, an injected DLL, or a small in-process helper) reading `GetWindowLong(hwnd, GWL_EXSTYLE)` and comparing against `WS_EX_TRANSPARENT`. That was out of scope here.
- The first opaque click test (Q2c) not showing captured behaviour surprised me until the timestamp analysis showed the DR bar had already expired before the AFTER screenshot. Tests D and E, run with a screencapture immediately after `cliclick`, both showed captured — I am confident the reproducible answer is "captured", not "passed through". I am NOT claiming the negative result is unconditional across all Wine versions, macOS versions, or GPU/compositor configurations; only that on Wine 11.0 / macOS 14.8.8 / this hardware, with the exact `OverlayClickThrough=true` code path the Mac build ships, opaque pixels captured clicks in every clean-timed test I ran.
- Text-to-speech, EverQuest fullscreen z-order interactions, and any behaviour under macOS 15 were not exercised — same non-coverage as `WINE-FINDINGS.md`.

## What this means for the Mac-build direction

Q1 (no regression on the settings window) and Q3 (updater inert) both pass. Those were the two "did I break something" questions.

Q2 was the one that would have justified the whole `OverlayClickThrough` feature on Wine, and the answer is that it does not deliver what it advertises on this platform. The code is correct and the setting reaches the right code path; the Wine layer just doesn't turn `WS_EX_TRANSPARENT` on an `AllowsTransparency="True"` WPF window into `setIgnoresMouseEvents:YES` on the underlying `NSWindow`. Shipping the Mac build in its current form is safe (Q1, Q3) but the flagship user-visible feature it adds on top of the stock behaviour is not observable to a Wine end-user. Any decision to promote the Mac build to Wine users on the strength of the click-through feature needs a real fix for that gap first — either a Wine patch, a Wine version bump that fixes it, or a different mechanism to achieve per-pixel click-through under Wine.
