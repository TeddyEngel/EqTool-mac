# mac/install.command test evidence

Evidence for the five tests specified in the installer task. All runs used an
isolated throwaway prefix (`$HOME/.wine-pigparse-installtest`) and a test
bundle name (`PigParseInstallTest`), so the user's real
`~/.wine-pigparse` and any `~/Applications/PigParse.app` were not touched.

Test invocation pattern:

```bash
PIGPARSE_YES=1 \
PIGPARSE_PREFIX="$HOME/.wine-pigparse-installtest" \
PIGPARSE_APP_NAME="PigParseInstallTest" \
  ./mac/install.command
```

## Environment

- macOS 14.8.8 Sonoma, Intel x86_64, Retina 5K
- Homebrew 6.0.21
- wine-11.0 at `/usr/local/bin/wine`
- winetricks at `/usr/local/bin/winetricks`
- Release under test: `5.26.830.1` (the pinned default in `install.command`)
- Download URL:
  `https://github.com/smasherprog/EqTool/releases/download/5.26.830.1/EQTool_Linux5.26.830.1.zip`
  (13,429,464 bytes)

## Test 1 — Cold-start install (PASS)

Test prefix and bundle removed before the run:

```
ls: /Users/teddyengel/.wine-pigparse-installtest: No such file or directory
ls: /Users/teddyengel/Applications/PigParseInstallTest.app: No such file or directory
```

Installer log final lines:

```
==> Installing PigParse into /Users/teddyengel/.wine-pigparse-installtest/drive_c/PigParse
    EQTool.exe on disk: 31125808 bytes.

==> Generating /Users/teddyengel/Applications/PigParseInstallTest.app
    Bundle written.

==> Done.
    PigParse 5.26.830.1 installed under /Users/teddyengel/.wine-pigparse-installtest.
    Bundle: /Users/teddyengel/Applications/PigParseInstallTest.app
```

Post-install disk state:

```
-rw-r--r-- ... /Users/teddyengel/.wine-pigparse-installtest/drive_c/windows/dotnet48.installed.workaround
-rw-r--r-- 31125808 ... /Users/teddyengel/.wine-pigparse-installtest/drive_c/PigParse/EQTool.exe
```

Wall time on this host: about 8 minutes 30 seconds. The .NET 4.8 step
dominates.

## Test 2 — Launch (PASS)

```bash
open "$HOME/Applications/PigParseInstallTest.app"
sleep 25
screencapture -x /tmp/01-launch.png
```

`ps aux` during the capture showed `C:\PigParse\EQTool.exe` at ~530 MB RSS
plus the usual `wineserver` and two `winedevice.exe` helpers, all owned by
`/Applications/Wine Stable.app/Contents/Resources/wine/lib/wine/../../bin/wineserver`.
A process alone is not evidence — the UI has to be visible.

Screenshot: `01-cold-launch.png` (downscaled with `sips -Z 1200`).

Inspected via `look_at`. Verbatim summary of what the inspection reported:

- Window title bar: **SettingManagement**.
- Tabs across the top: **General** (active) | Text & Audio Alerts | Triggers
  | Characters | Experimental | IE | Friends. (Upstream renders the "UI" tab
  label as "IE" under this Wine build.)
- Red **Configuration missing!** banner at the top of the General tab
  content area.
- **Eq Path** label with an adjacent **Browse** button (folder icon).
- **Eq Log Path** label with the red text **You must enable loggging!**
  next to it. (The triple-g is upstream.)

That matches the expected first-run state documented in
`spike/WINE-FINDINGS.md` section 3.

## Test 3 — Idempotency (PASS)

Second run against the same prefix, immediately after test 2, with no state
changes in between. Full installer log:

```
==> Checking for Homebrew
    Found: /usr/local/bin/brew
==> Checking for Wine
    wine already present at /usr/local/bin/wine: wine-11.0
==> Checking for winetricks
    winetricks already present at /usr/local/bin/winetricks
==> Preparing Wine prefix: /Users/teddyengel/.wine-pigparse-installtest
    Prefix already exists, keeping it.
==> Checking for .NET Framework 4.8 in prefix
    Already installed (marker + mscorlib.dll present), skipping.
==> Resolving PigParse release
    Using release: 5.26.830.1
==> Downloading EQTool_Linux5.26.830.1.zip
    Downloaded 13429464 bytes.
    Verifying zip integrity...
==> Installing PigParse into /Users/teddyengel/.wine-pigparse-installtest/drive_c/PigParse
    EQTool.exe on disk: 31125808 bytes.
==> Generating /Users/teddyengel/Applications/PigParseInstallTest.app
    Removing existing bundle first.
    Bundle written.
==> Done.
```

Total wall time: 2.9 seconds (versus ~8 minutes 30 seconds on cold-start).
The dotnet48 step was skipped as designed. Exit code 0.

## Test 4 — Uninstall (PASS)

```bash
PIGPARSE_YES=1 \
PIGPARSE_PREFIX="$HOME/.wine-pigparse-installtest" \
PIGPARSE_APP_NAME="PigParseInstallTest" \
  ./mac/uninstall.command
```

Output:

```
==> PigParse macOS uninstaller
    Wine prefix:  /Users/teddyengel/.wine-pigparse-installtest
    App bundle:   /Users/teddyengel/Applications/PigParseInstallTest.app
!!! This deletes your PigParse settings, triggers, saved sessions, and log parser state.
    It does NOT remove Homebrew, wine-stable, or winetricks.
    It does NOT touch ~/.wine or any CrossOver bottle.
==> Removing /Users/teddyengel/.wine-pigparse-installtest
==> Removing /Users/teddyengel/Applications/PigParseInstallTest.app
==> Done.
```

Post-uninstall check:

```
ls: /Users/teddyengel/.wine-pigparse-installtest: No such file or directory
ls: /Users/teddyengel/Applications/PigParseInstallTest.app: No such file or directory
```

## Test 5 — Real prefix untouched (PASS)

Ran after all four tests above completed.

```
-rw-r--r-- 31125808  4 Sep 13:46 /Users/teddyengel/.wine-pigparse/drive_c/PigParse/EQTool.exe
-rw-r--r--  5676080 28 Mar  2019 /Users/teddyengel/.wine-pigparse/drive_c/windows/Microsoft.NET/Framework/v4.0.30319/mscorlib.dll
-rw-r--r--        0  4 Sep 13:43 /Users/teddyengel/.wine-pigparse/drive_c/windows/dotnet48.installed.workaround
```

The `EQTool.exe` and `dotnet48.installed.workaround` timestamps
(`Sep 4 13:46` and `Sep 4 13:43`) are the same ones recorded during the
original spike, well before this task started. The real prefix was not
touched by any of the four preceding tests.

## Committed evidence file

- `01-cold-launch.png` — full-screen capture 25 seconds after
  `open ~/Applications/PigParseInstallTest.app`. The SettingManagement window
  is visible in the top-left with all seven tabs, the red
  "Configuration missing!" banner, the Eq Path and Eq Log Path fields, and
  the "You must enable loggging!" validation text. Downscaled to 1200 px
  wide with `sips -Z 1200`, matching the convention of
  `spike/evidence-wine/`.

Only one screenshot is committed because the other four tests are exercised
through their command output rather than through pixels. The uninstall test
in particular has no meaningful visual state to capture.
