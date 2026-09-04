# PigParse on macOS

PigParse parses your EverQuest log file to show spell timers, triggers, DPS meters, and maps while you play on Project 1999 or Quarm. This repository maintains macOS support for running the application under Wine.

## Current status

The application runs under Wine on macOS. That much has been checked directly: it installs, launches, renders its interface, reads a log file, and draws its overlays above other Wine windows.

What has not been checked matters just as much:

- Nobody has run this against a real EverQuest client yet. The testing used a hand-written log file and Wine's own configuration dialog standing in for the game. Expect rough edges on first contact with the actual client.
- Text-to-speech does not work. Upstream builds for Linux and macOS strip `System.Speech`, so triggers set to speak will stay silent. Visual alerts and audio sound files still work normally.
- Overlay windows pass mouse clicks through only on fully transparent pixels. Any opaque content, such as a timer bar, receives the click instead of passing it to the game behind it.

## Prerequisites

You need Homebrew and Wine. Testing was done on macOS 14.8.8 Sonoma on Intel x86_64. Other versions and Apple Silicon may well work, but nobody has tried.

Install Wine and Winetricks using Homebrew:

```bash
brew install wine-stable winetricks
```

## Setup

Use a dedicated Wine prefix to avoid altering your main EverQuest or CrossOver configuration.

1. Create and initialize an isolated 64-bit Wine prefix.

```bash
export WINEPREFIX="$HOME/.wine-pigparse"
export WINEARCH=win64
wineboot -u
```

2. Force Winetricks to use `curl` for file downloads.

```bash
export WINETRICKS_DOWNLOADER=curl
```

The default `wget` path broke during testing due to Homebrew library version mismatches. Setting `curl` prevents this failure mode.

3. Install .NET Framework 4.8 into the prefix.

```bash
winetricks -q dotnet48
```

This installation takes about 9 minutes. The progress bar may appear to hang during runtime registration steps, but the process is still running. Allow it to finish.

4. Download and extract the upstream release package.

Download the `EQTool_Linux*.zip` package from the [upstream releases page](https://github.com/smasherprog/EqTool/releases). Extract the files into your prefix directory:

```bash
mkdir -p "$WINEPREFIX/drive_c/PigParse"
unzip EQTool_Linux*.zip -d "$WINEPREFIX/drive_c/PigParse"
```

5. Start the application.

```bash
WINEPREFIX="$HOME/.wine-pigparse" wine "$HOME/.wine-pigparse/drive_c/PigParse/EQTool.exe"
```

## First-run configuration

On first launch, the application displays a red "Configuration missing!" banner. It cannot auto-detect your EverQuest installation path because Windows system paths do not match macOS file locations.

Point the application to your EverQuest directory manually in the settings window. Wine maps your Mac root directory to drive letter `Z:`.

- If EverQuest is installed inside this same Wine prefix, set the path to `C:\EQ` (or your chosen path inside `drive_c`).
- If EverQuest runs in a separate CrossOver bottle or different Wine prefix, browse through drive `Z:` to select your Mac directory path, such as `Z:\Users\yourusername\Library\Application Support\CrossOver\Bottles\EverQuest\drive_c\EverQuest`.

You must also turn on logging in EverQuest itself. PigParse reads nothing but the log file, so with logging off it sits there doing nothing.

The settings window has a button that writes `Log=TRUE` into your `eqclient.ini` for you. Use it. The one case where it cannot help is an EverQuest installed under `Program Files`, where Wine's permissions get in the way — the app will tell you so, and you then add `Log=TRUE` to `eqclient.ini` by hand.

Typing `/log on` in game works too, but only for that session.

To map your position accurately on PigParse maps, set up a location macro. Create an in-game hotkey containing `/loc` and bind it to a common movement key such as `A` or `D`.

## System tray operation

When launched on macOS under Wine, PigParse minimizes to the macOS menu bar near the top right of your screen. Look for a small pig icon.

Closing main windows does not exit the application. Right-click the pig menu bar icon to reopen the settings window, trigger overlay, DPS meter, or map. Use the Exit option in that menu to close the program completely.

## Troubleshooting

### .NET Framework 4.8 installer appears stuck

The `.NET` installation phase through `winetricks` installs `dotnet40` before applying `dotnet48`. The installer pauses for several minutes while registering DLL assemblies. Do not close the terminal window; it finishes automatically after roughly 9 minutes.

### Application shows "Configuration missing!" banner

This banner means PigParse has not found `eqgame.exe` or valid log files. Open the General tab in settings and verify that `Eq Path` and `Eq Log Path` point to valid directories. Drive `Z:` gives access to your Mac files outside the Wine prefix.

### Timers and DPS meters do not update

Confirm that EverQuest is actively writing to a log file. Check that `eqclient.ini` contains `Log=TRUE` or type `/log on` in game. Verify that new lines appear in your log file located in `Logs/eqlog_Character_Server.txt`.

### Finding errors

PigParse writes failures to `Errors.txt` next to the executable. Your terminal session also carries Wine's own output, which is usually noisier but occasionally more revealing.

```bash
cat "$HOME/.wine-pigparse/drive_c/PigParse/Errors.txt"
```

Your configuration lives in `settings.json` in the same folder. Deleting that file resets the app to first-run state, which is a quick way out of a broken configuration.

### The app replaced itself

The stock build checks upstream for new releases and updates itself. That is normal and generally what you want. It does mean a version you had working can change under you — if something breaks after a restart, an update is a reasonable first suspect.

## Uninstalling

To remove PigParse and its runtime dependencies, delete the dedicated Wine prefix directory:

```bash
rm -rf "$HOME/.wine-pigparse"
```
