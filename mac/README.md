# PigParse on macOS

PigParse parses your EverQuest log file to show spell timers, triggers, DPS
meters, and maps while you play on Project 1999 or Quarm. This directory
maintains macOS support for running it under Wine.

## Current status

The application runs under Wine on macOS. It installs, launches, renders its
interface, reads a log file, and draws its overlays above other Wine windows.
The details of that verification are in `spike/WINE-FINDINGS.md`.

What has not been checked matters just as much:

- Nobody has run this against a real EverQuest client yet. The testing used a
  hand-written log file and Wine's own configuration dialog standing in for
  the game. Expect rough edges on first contact with the actual client.
- Text-to-speech does not work. The Linux and macOS builds strip
  `System.Speech`, so triggers set to speak stay silent. Visual alerts and
  audio sound files still work.
- Overlay windows pass mouse clicks through only on fully transparent pixels.
  Any opaque content, such as a timer bar, receives the click instead of
  passing it to the game behind it.

## What this sends over the network

Under Wine you run the ordinary Windows build, so it does what it does on
Windows. Several of those things happen on their own, and the interface announces
none of them.

The program starts a twenty second timer when it launches, and once it has read
enough of the log to know which server you are on, each tick posts character data
to `pigparse.azurewebsites.net`. The location sharing setting does not decide
whether that request goes out. It rides inside the request as a field, and the
request is sent either way.

Conning something sends more than the con. The mob info lookup posts the mob name
and your current zone, which is what fills the window in. A separate service posts
the mob name, your zone, and your coordinates, read from the last `/loc` you
typed. It checks that the server is known and nothing else. Deaths go out the same
way.

The program posts boat sightings and earthquakes as it parses them. When a first
engage message appears, it asks the service about that player by name. While the
timers window is open it fetches other players' boat sightings and roll timers.

The program posts its errors to the same service. The message is the exception
text, which routinely contains file paths, and on macOS those paths contain your
account name.

Inventory sync, UI file sync, and Discord login also use the service, but you
switch those on yourself, so they are not surprises.

None of this is a fault in the macOS packaging. Windows users get all of it. It is
written down here because the sharing setting reads as though it decides what
leaves your machine, and it decides very little of it.

This build changes one thing: the updater is switched off. It fetches Windows
release archives, and there is no macOS release for it to find.
`spike/WINE-FINDINGS.md` has the details.

The native client in `native/` blocks almost all of it. It allows the mob info
wiki lookup and refuses the other six endpoints its code can reach, so item
prices, player lookups, boat sharing and roll timers come back empty instead of
failing. The service that reports your coordinates is not built into it at all.
That guard does not apply here, because the Wine path runs the upstream binary
instead of the native code.

All of this comes from reading the code rather than watching the traffic.

## Quick install

You need a Mac with Homebrew already installed. The installer will not install
Homebrew for you. Get it first from [brew.sh](https://brew.sh) if you don't
have it.

Clone this repo and run the installer:

```bash
git clone https://github.com/smasherprog/EqTool.git
cd EqTool
./mac/install.command
```

Or open Finder, browse to `mac/` in the cloned repo, and double-click
`install.command`. Terminal will open and run it.

The installer:

1. Checks for Homebrew.
2. Installs `wine-stable` and `winetricks` through Homebrew if they aren't
   already present.
3. Creates a dedicated Wine prefix at `~/.wine-pigparse`. Your `~/.wine` and
   CrossOver bottles are not touched.
4. Installs .NET Framework 4.8 into that prefix. This step takes about nine
   minutes and looks frozen for long stretches. It is not frozen. Leave it.
5. Downloads a pinned PigParse release (currently `5.26.830.1`) and extracts
   it into the prefix.
6. Generates `~/Applications/PigParse.app` on your machine.

When it's finished, launch PigParse from Finder like any other app, or with
`open ~/Applications/PigParse.app` from the terminal.

To uninstall:

```bash
./mac/uninstall.command
```

That removes the Wine prefix and the app bundle. It leaves Homebrew,
`wine-stable`, and `winetricks` alone in case you want them for other things.

### Why the installer builds the .app instead of shipping one

macOS puts a quarantine attribute on any bundle downloaded through a browser
and, for unsigned apps, App Translocation runs it from a randomised read-only
path. That breaks relative paths and self-writes, which PigParse does. Files
obtained through `git clone` are not quarantined, so the installer itself
runs. The `.app` it generates on your machine has no quarantine attribute
either, and Translocation leaves it alone.

If you downloaded this repo as a zip through a browser instead of cloning it,
macOS will refuse to run the script until you clear the quarantine flag:

```bash
xattr -d com.apple.quarantine mac/install.command
xattr -d com.apple.quarantine mac/uninstall.command
```

### Installer options

The installer reads a few environment variables:

- `PIGPARSE_VERSION` — release tag to install. Defaults to the pinned version
  in the script. Set to `latest` to resolve the newest release from the
  GitHub API. Print the resolved version and download URL before starting.
- `PIGPARSE_PREFIX` — Wine prefix directory. Defaults to
  `$HOME/.wine-pigparse`. Change it if you want a throwaway prefix for
  testing.
- `PIGPARSE_APP_NAME` — name of the generated bundle without the `.app`
  suffix. Defaults to `PigParse`. Mirror `PIGPARSE_PREFIX` for isolated test
  runs.
- `PIGPARSE_YES=1` — skip the initial confirmation prompt.

Rerunning the installer against an existing prefix is safe. It detects that
.NET 4.8 is already installed (via the winetricks marker file and
`mscorlib.dll`) and skips that nine-minute step. It re-downloads the release
zip and re-extracts, which lets you upgrade PigParse without a full reinstall.

## First-run configuration

The first launch shows a red "Configuration missing!" banner. PigParse can't
auto-detect your EverQuest install because Wine paths and Mac paths don't
match what the Windows detection code expects.

Point PigParse at your EverQuest directory in the settings window. Wine maps
your Mac root to drive `Z:`.

- If EverQuest is installed inside this same Wine prefix, set the path to
  `C:\EQ` (or wherever you put it inside `drive_c`).
- If EverQuest runs in a separate CrossOver bottle or a different Wine
  prefix, browse through `Z:` to select the path on your Mac, for example
  `Z:\Users\yourusername\Library\Application Support\CrossOver\Bottles\EverQuest\drive_c\EverQuest`.

You also need to turn on logging in EverQuest itself. PigParse reads nothing
but the log file, so with logging off it just sits there.

The settings window has a button that writes `Log=TRUE` into your
`eqclient.ini` for you. Use it. The one case where it can't help is an
EverQuest installed under `Program Files`, where Wine's permissions get in
the way — the app will say so, and you then add `Log=TRUE` to `eqclient.ini`
by hand.

Typing `/log on` in game also works, but only for that session.

To map your position accurately on PigParse maps, make an in-game hotkey that
contains `/loc` and bind it to a movement key such as `A` or `D`.

## System tray

Under Wine on macOS, PigParse minimizes to the macOS menu bar near the top
right of the screen. Look for a small pig icon.

Closing the main windows does not exit the application. Right-click the pig
icon to reopen the settings window, trigger overlay, DPS meter, or map. Use
`Exit` in that menu to close PigParse for good.

## Troubleshooting

### .NET Framework 4.8 installer looks stuck

`winetricks` installs `dotnet40` first, then applies `dotnet48`. The installer
pauses for several minutes while registering DLL assemblies. Don't close the
window. It finishes on its own after roughly nine minutes.

### "Configuration missing!" banner

PigParse hasn't found `eqgame.exe` or a valid log file. Open the General tab
in settings and check that `Eq Path` and `Eq Log Path` point to real
directories. Drive `Z:` gets you at Mac files outside the Wine prefix.

### Timers and DPS don't update

Confirm EverQuest is writing to a log file. Check that `eqclient.ini`
contains `Log=TRUE` or type `/log on` in game. Look for new lines in
`Logs/eqlog_<Character>_<Server>.txt`.

### Finding errors

PigParse writes failures to `Errors.txt`. The file appears only once
something has actually gone wrong, so its absence is a good sign. It lands
in whatever directory the app was launched from — for the generated `.app`
that's `~/.wine-pigparse/drive_c/PigParse`, because the launcher `cd`s
there before running Wine.

```bash
cat "$HOME/.wine-pigparse/drive_c/PigParse/Errors.txt"
```

Your configuration lives in `settings.json` in the same folder. Deleting
that file resets the app to first-run state, which is a quick way out of a
broken configuration.

### The app replaced itself

The stock build checks upstream for new releases and updates itself. That's
normal and generally what you want, but it also means a version you had
working can change under you. If something breaks after a restart, an update
is a reasonable first suspect.

### Homebrew's `wget` fails during .NET install

This is why the installer sets `WINETRICKS_DOWNLOADER=curl`. Homebrew's
`wget` on this host was linked against `libunistring.2.dylib` and the
installed libunistring was `.5`, so every download died with a
`dyld: Library not loaded` error. If you were running `winetricks` yourself,
`brew reinstall wget` would fix it. Through the installer, `curl` sidesteps
the problem entirely.

## Manual install

Use this section if you'd rather run each step yourself, or if the installer
fails partway and you want to pick up from where it stopped. It's exactly
what the installer does.

### Prerequisites

You need Homebrew and Wine. Testing was done on macOS 14.8.8 Sonoma on
Intel x86_64. Other versions and Apple Silicon may well work, but nobody has
tried.

Wine ships as a Homebrew cask and Winetricks as a formula, so they install
with two separate commands. Running them together fails.

```bash
brew install --cask wine-stable
brew install winetricks
```

### Setup

Use a dedicated Wine prefix to avoid touching your main EverQuest or
CrossOver configuration.

1. Create and initialize an isolated 64-bit Wine prefix.

   ```bash
   export WINEPREFIX="$HOME/.wine-pigparse"
   export WINEARCH=win64
   wineboot -u
   ```

2. Force Winetricks to use `curl` for downloads.

   ```bash
   export WINETRICKS_DOWNLOADER=curl
   ```

   The default `wget` path broke during testing because of a Homebrew
   library version mismatch. `curl` prevents that failure mode.

3. Install .NET Framework 4.8 into the prefix.

   ```bash
   winetricks -q dotnet48
   ```

   About nine minutes. The progress bar may appear to hang during runtime
   registration. Leave it.

4. Download and extract the upstream release.

   Get `EQTool_Linux5.26.830.1.zip` (or a newer version) from the
   [releases page](https://github.com/smasherprog/EqTool/releases), then:

   ```bash
   mkdir -p "$WINEPREFIX/drive_c/PigParse"
   unzip EQTool_Linux*.zip -d "$WINEPREFIX/drive_c/PigParse"
   ```

5. Start the application.

   `cd` into the app folder first. PigParse writes `Errors.txt` relative to
   whatever directory you launch it from, so starting it elsewhere scatters
   those files around.

   ```bash
   cd "$WINEPREFIX/drive_c/PigParse"
   wine EQTool.exe
   ```

Follow the [First-run configuration](#first-run-configuration) section above
from there.

### Manual uninstall

Delete the dedicated Wine prefix:

```bash
rm -rf "$HOME/.wine-pigparse"
```
