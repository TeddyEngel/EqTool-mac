# PigParse native macOS port — Milestone 1 notes

This directory holds the additive `.NET 9` port of PigParse's platform-agnostic
core. Nothing in `EQTool/`, `EQToolShared/`, `EQToolApis/`, or `EQtoolsTests/`
is edited; upstream files are pulled in with `<Compile Include="..." Link="..."/>`
so `git merge upstream/master` stays a clean fast-forward.

## What compiles on `net9.0` today

- `native/EQTool.Core` — class library, target `net9.0`.
- `native/EQTool.Core.Tests` — MSTest project, target `net9.0`.

Linked source:

| Tree | Files | Lines |
|---|---|---|
| `EQToolShared/**/*.cs` (all, minus `Properties/AssemblyInfo.cs`) | 28 | 4188 |
| `EQTool/Services/Parsing/**/*.cs` (all) | 35 | 2679 |
| `EQTool/Models/*.cs` — 16 individually linked | 16 | 3187 |
| `EQTool/Services/*.cs` and `EQTool/Services/Spells/*.cs` — 5 individually linked | 5 | 1176 |
| `EQTool/Services/TypeConverters/EnumDescriptionTypeConverter.cs` | 1 | 34 |
| `EQTool/ViewModels/` — `ActivePlayerInfo.cs`, `ConsoleViewModel.cs`, `MobInfoComponents/MobInfoViewModel.cs` | 3 | 718 |

That is 88 upstream files, ~11 982 lines, all built against `net9.0` with
`Nullable=disable` and `ImplicitUsings=disable`, zero warnings, zero errors.

Static data:

- `EQToolShared/Properties/Resources.resx` is added as `<EmbeddedResource>` with
  `LogicalName=EQToolShared.Properties.Resources.resources` so upstream's
  strongly-typed `Resources.Designer.cs` finds `MasterItemList`,
  `MasterNPCList`, and `items_vendor_prices` without editing anything.

Package pins (exact versions, no ranges):

- `Newtonsoft.Json 13.0.4` (matches upstream's `packages.config`).
- Test SDK: `Microsoft.NET.Test.Sdk 17.11.1`, `MSTest.TestAdapter 3.6.4`,
  `MSTest.TestFramework 3.6.4`.

## Shims (`Compat/`)

Three files, all in namespaces the upstream code already imports. Declaring
types inside `System.*` namespaces is legal C# and is the same technique the
.NET runtime team uses for polyfill packages.

### `Compat/Point3D.cs`
Declares `System.Windows.Media.Media3D.Point3D` as a `struct` with settable
`double X, Y, Z`, value semantics, and a culture-invariant `ToString`. Chosen
because:
- `LocationParser.cs:51` uses `new Point3D { X = .., Y = .., Z = .. }` (object
  initialiser → needs a parameterless ctor and settable properties).
- `EventModels.cs:242` declares `Point3D?` (nullable). WPF's `Point3D` is a
  value type, so the shim must be a `struct` for `?` to mean "nullable value
  type" rather than "nullable reference type".

### `Compat/WindowsShims.cs`
Minimal declarations for the WPF types upstream references from files we still
need to compile:
- `System.Windows.Visibility`, `WindowState`, `Rect`, `Int32Rect`
- `System.Windows.Media.Brush` (with no-op `Freeze()`/`CanFreeze`),
  `SolidColorBrush`, `Color`, `BrushConverter`, `Brushes` (named-brush singletons)
- `System.Windows.Media.ImageSource`
- `System.Windows.Media.Imaging.BitmapCacheOption`, `BitmapSource`, `BitmapImage`,
  `CroppedBitmap`

Nothing here paints pixels, allocates GPU resources, or reads bitmap data. It
exists purely to satisfy the compiler for types the parsers store in fields
but never actually render.

### `Compat/EqToolStubs.cs`
Type-name stubs (not shims — different upstream namespaces than `System.*`) for
the small set of upstream service classes whose *implementations* pull in
un-portable dependencies. The parsers reference the *types* via constructor
injection but do not exercise the affected code paths at test time:

- `EQTool.App` — WPF `App` singleton. Only `Version` / `VersionType` are read
  from linked files (`MobInfoViewModel.cs`, and indirectly `LoggingService`
  which we do not link).
- `EQTool.Services.IAppDispatcher` + `AppDispatcher` — real upstream targets
  WPF's `Dispatcher` via `App.Current.Dispatcher`. Synchronous no-op is fine
  for a headless core.
- `EQTool.Services.BinarySerializer` — real upstream uses `BinaryFormatter`,
  which is removed from .NET 9. The parsers never call it; only settings load
  does, and we do not link that path.
- `EQTool.Services.LoggingService` — real upstream POSTs crash reports over
  HTTP and reads WPF `App.Version`. A no-op `Log()` is what a headless core
  wants anyway.
- `EQTool.Services.SpellIcons` — real upstream decodes embedded `.tga` icons
  through `TGASharpLib` and `System.Drawing.Bitmap`. Neither is desirable in a
  headless core. The stub returns an empty `List<SpellIcon>`, which means
  `EQSpells.BuildSpellInfo` treats every entry as `HasSpellIcon == false` if
  you call it. Tests that need populated spell lookups will need a Milestone 2
  replacement (see Follow-ups).

## Files deliberately NOT linked (and why)

| Upstream file | Reason |
|---|---|
| `EQToolShared/Properties/AssemblyInfo.cs` | SDK generates its own assembly attributes; keeping upstream's would cause `CS0579: duplicate 'AssemblyCompanyAttribute'` etc. `GenerateAssemblyInfo` is also `false`, but the exclusion is what stops the file's attributes from double-declaring. |
| `EQTool/Services/AppDispatcher.cs` | Uses `App.Current.Dispatcher.Thread` and `App.Current.Dispatcher.Invoke`. Replaced by `Compat/EqToolStubs.cs` synchronous stub. |
| `EQTool/Services/LoggingService.cs` | Reads WPF `App.Version` and POSTs to `pigparse.azurewebsites.net` at every log call. Replaced by no-op stub in `Compat/EqToolStubs.cs`. |
| `EQTool/Services/BinarySerializer.cs` | `System.Runtime.Serialization.Formatters.Binary.BinaryFormatter` is removed in .NET 9. Replaced by throw-on-use stub. |
| `EQTool/Services/Spells/SpellIcons.cs` | Uses `TGASharpLib` (unavailable) and `System.Drawing.Bitmap` (needs `System.Drawing.Common` and is macOS-hostile). Replaced by empty-list stub. |
| All other files under `EQTool/UI/`, `EQTool/Services/Map/`, `EQTool/Services/Handlers/`, `EQTool/Services/P99LoginMiddlemand/`, `EQTool/Services/MarkupExtensions/`, `EQTool/Services/IO/`, `EQTool/ViewModels/SettingsComponents/`, `EQTool/ViewModels/SpellWindow/` etc. | UI / networking / dispatcher-heavy code that is not part of the parsing core. Milestone 1 is scoped to parsers. |

## Verification

`dotnet build native/EQTool.Core -warnaserror` (repeated after the last change):

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`dotnet test native/EQTool.Core.Tests`:

```
Passed!  - Failed:     0, Passed:    13, Skipped:     0, Total:    13, Duration: 460 ms
```

Test breakdown (13 total):

- `DamageParserTests` (5): you-hits, other-hits, non-melee redirect,
  you-misses, unrelated-line-returns-null. Exercises the regex family end to
  end.
- `LocationParserTests` (4): `/loc` line raises `PlayerLocationEvent` with the
  parsed `Point3D`, unrelated line returns false, direct shim object-initialiser
  round-trip, direct shim nullable round-trip. These are the tests that would
  fail immediately if the `Point3D` shim were wrong.
- `YouZonedParserTests` (4): `You have entered Temple of Veeshan.` maps to the
  short name `templeveeshan` via `Zones.TranslateToMapName`, arena PvP notice
  returns null, unrelated line returns null, `Handle` raises `YouZonedEvent`
  with the correct short name and line counter.

Expected values come from reading the parser regex and `Zones.cs`, not from
guessing at log formats.

`git status --porcelain` shows only `.omo/` (session state) and `native/` (new
work) as untracked:

```
?? .omo/
?? native/
```

`git diff --stat HEAD -- EQTool EQToolShared EQToolApis EQtoolsTests EqTool.sln README.md LICENSE`:

```
(empty)
```

Zero upstream files changed. `EqTool.sln` is not modified and the new projects
are not added to it, per the milestone constraints.

## What surprised me

- `EQToolShared` really is 100% portable — 28 files, 4 194 lines, zero
  `System.Windows` imports, dropped straight into a `net9.0` compile with no
  changes. That is the strongest signal in this milestone that the port
  strategy is sound.
- `Services/Parsing/` was almost as clean. The one WPF import
  (`System.Windows.Media.Media3D` in `LocationParser.cs`) resolves against
  the shim and compiles with zero touches.
- The blast radius from `Models/EventModels.cs` `Brush` fields was smaller
  than expected — declaring a marker `Brush` type in `System.Windows.Media`
  satisfies `OverlayEvent.ForeGround`/`TimerBarEvent.BarColor` completely.
- `MasterNPCList` and friends load from an embedded `.resources` name that
  the SDK-style project system happily rebuilds from the linked `.resx` as
  long as the `LogicalName` matches. This avoids re-embedding the raw `.txt`
  files.

## Upstream test suite

`EQtoolsTests/` has 42 `.cs` files. 2 are infrastructure (`BaseTestClass.cs`,
`DI.cs`), 39 carry `[TestMethod]`s, and 1 (`AssemblyBindingFixture.cs`) is
.NET Framework binding-redirect machinery. All 41 non-fixture files are
linked into `native/EQTool.Core.Tests` unedited via
`<Compile Include="..\..\EQtoolsTests\...cs" Link="Upstream\..." />`.

To make the linked set compile and run, the following additions were made:

- `EQTool.Core` linked-set extension: `EQTool/Services/Handlers/**/*.cs`,
  `LogParser.cs`, `EQToolSettingsLoad.cs`, `FindEq.cs`, `BoatScheduleService.cs`,
  `PigParseApi.cs`, `AudioService.cs`, `SavePlayerStateService.cs`,
  `InstallPathChecker.cs`, `PlayerTrackerService.cs`, `TriggerActionExecutor.cs`,
  `TriggerTimerManager.cs`, `TriggerTestSampleGenerator.cs`, `CHService.cs`,
  `WikiApi.cs`, `IO/FileReader.cs`, `Spells/SpellUIExtensions.cs`,
  the real `SpellWindowViewModel.cs`, `SpawnTimerDialogViewModel.cs`,
  `DPSWindowViewModel.cs`, `EntittyDPS.cs`, `PetViewModel.cs`,
  `SpellWindow/**/*.cs`, and `Models/Pets.cs`.
- `Compat/WindowsShims.cs` was extended with the WPF surface these files
  reference: the full `Brushes` palette used across handlers/viewmodels
  (with `Brushes.*` typed as `SolidColorBrush` to satisfy assignments to
  `BaseTriggerViewModel.ProgressBarColor`), `Colors`, `Point`,
  `LinearGradientBrush`, `GradientStop`, `GradientStopCollection`,
  `MediaPlayer`, `System.Windows.Data.CollectionViewSource` +
  `ListCollectionView` + `PropertyGroupDescription` + `SortDescription` +
  `IValueConverter` + `Binding.DoNothing`, `System.Windows.Controls.ValidationRule`
  + `ValidationResult` + `UserControl`, `System.Windows.Threading.DispatcherTimer`,
  and an empty `System.Windows.Documents` namespace so unused `using`s resolve.
  The `BrushConverter` shim now parses named colors (via a
  `NamedColors` table covering every value used in the codebase) and `#RRGGBB` /
  `#AARRGGBB` hex, so `TriggerColors.ToBrush("HotPink", ...)` returns the
  actual pink and not a default-constructed `SolidColorBrush`.
- `Compat/EqToolStubs.cs` was extended with `App.httpclient` (real
  `HttpClient`, so `PigParseApi`'s try/catch swallows network failures
  cleanly at test time), a no-op `App.LogUnhandledException`, an `ITextToSpeach`
  interface (so `BaseHandler`'s `textToSpeach` parameter binds — the real
  `TextToSpeach.cs` uses `System.Speech.Synthesis.SpeechSynthesizer` which
  needs the Windows-only `System.Speech` reference), a `ForegroundWindowHelper`
  stub whose `IsEqGameFocused()` returns `false` (real one calls `user32.dll`
  via P/Invoke), a `SpellIcons` stub that returns one `SpellIcon` per spell
  file (indices 1..7) so `EQSpells.BuildSpellInfo` no longer skips every
  spell as `HasSpellIcon == false`, and minimal `MobInfoManagementViewModel`
  / `SettingsWindowViewModel` / `MobInfoItemType` shims that expose only the
  members the linked handlers touch (respectively `MobInfoItemType` for
  `ConHandler` and `GroupLeaderName` for `GroupLeaderHandler`).
- Test-resource plumbing: `spells_us.txt`, `DiscordResponse.json`, and
  `LogFiles/log1.txt` are `Content Include`'d from `EQtoolsTests/`, and an
  `AfterTargets="Build"` target copies them one level up from
  `bin/Debug/net9.0/` to `bin/`. This mirrors where upstream's
  `DI.cs:99` (`Directory.GetParent(Paths.ExecutableDirectory()).Parent.FullName`)
  points `EQToolSettings.DefaultEqDirectory`.
- MSTest was bumped from 3.6.4 to 4.0.2 (and `Microsoft.NET.Test.Sdk` from
  17.11.1 to 18.0.1) to match `EQtoolsTests/packages.config`. The upstream
  tests use `Assert.HasCount(...)` and `Assert.IsNotEmpty(...)`, which are
  only in MSTest 4.
- `Autofac 8.4.0` is pinned exactly, matching upstream.

### Results

```
Passed: 398, Failed: 28, Skipped: 0, Total: 426, Duration: 46 s
```

The 13 hand-written tests from Milestone 1 are still linked and still pass.
The remaining 413 are from the upstream suite.

Failure categorisation:

| Category | Count | Notes |
|---|---|---|
| (a) genuine port bug | 0 | |
| (b) inherently Windows-only, excluded from link | 0 | All 41 non-fixture files link and run |
| (c) culture / environment divergence | 28 | Detailed below |

### Files excluded from linking

| Upstream file | Reason |
|---|---|
| `EQtoolsTests/AssemblyBindingFixture.cs` | The file's own header comment says it exists because "MSTest v4 no longer runs .NET Framework tests in a per-assembly AppDomain, so the binding redirects in `app.config` are never applied." It installs a .NET Framework `AppDomain.CurrentDomain.AssemblyResolve` handler to serve dependency DLLs from the output folder. `net9.0` uses `AssemblyLoadContext`, not `AppDomain`, and the SDK-style build already places every dependency where it can be resolved. There is nothing this fixture would fix here. |

### Category (c) — culture / environment divergence

All 28 remaining failures are in this bucket. None indicate the parser
disagrees with itself between Windows and macOS; they indicate the test
data is Windows-shaped and passes through platform-conditional APIs.

**`PathsTests.cs` — 27 failures.**
`SimplePathCombineTests` × 9, `ProgramPathCombineTests` × 9, and
`DirectoryCombineTests` × 9 all exercise `EQToolShared.Extensions.Paths.Combine`
with Windows-style inputs (e.g. `"C:\\Everquest\\"`, `"\\eqclient.ini"`),
and their `Assert.AreEqual` expectations are built as
`$"C:{Path.DirectorySeparatorChar}Everquest{Path.DirectorySeparatorChar}eqclient.ini"`.
`Paths.Combine` does:

```csharp
return path1.Trim().TrimEnd(Path.DirectorySeparatorChar)
                   .TrimEnd(Path.AltDirectorySeparatorChar)
    + Path.DirectorySeparatorChar
    + path2.Trim().TrimStart(Path.DirectorySeparatorChar)
                  .TrimStart(Path.AltDirectorySeparatorChar);
```

On Windows: `DirectorySeparatorChar = '\\'`, `AltDirectorySeparatorChar = '/'`.
Both slashes get trimmed off the input, the join uses `\\`, and the expected
string is `"C:\\Everquest\\eqclient.ini"` — which matches.

On macOS/.NET 9: `DirectorySeparatorChar = '/'`, `AltDirectorySeparatorChar = '/'`.
`\` is not a separator, so `TrimEnd`/`TrimStart` do not touch it; the join
uses `/`. `Paths.Combine("C:\\Everquest\\", "eqclient.ini")` therefore
returns `"C:\\Everquest\\/eqclient.ini"` while the test expects
`"C:/Everquest/eqclient.ini"`.

Both outputs are correct for the platform they run on. On macOS the parser
will never see a `C:\...` path (log lines carry the log directory the user
configured, which will be a POSIX path), so the divergence is confined to
this test's synthetic Windows-shaped inputs. It is worth flagging for the
Milestone 2 native settings loader: if the settings JSON round-trips a
saved Windows install path onto macOS, `Paths.Combine` will produce a mixed
`\`/`/` string. The join code is fine for POSIX-shaped inputs.

**`UIFileNameTests.cs::ParsesFullPath` — 1 failure.**
`UIFileName.TryParse` uses `System.IO.Path.GetFileName` to strip a leading
directory from `"C:\\Everquest\\UI_Pigy_P1999Green.ini"`. On Windows,
`GetFileName` splits on both `\` and `/` and returns
`"UI_Pigy_P1999Green.ini"`. On macOS, `GetFileName` only splits on `/`, so
the whole string is treated as the "file name" and the parser then extracts
`"C:\\Everquest\\UI_Pigy"` as the player name. Same shape as the
`PathsTests` divergence: the code's contract quietly says "call me with
paths using the current platform's separators", the test's input assumes
Windows. On macOS the code will only ever be handed POSIX paths, so this is
also a test-data mismatch rather than a functional bug.

### What surprised me

- The single change that took failures from 145 to 31 was placing
  `spells_us.txt` and `DiscordResponse.json` at
  `bin/` (via the `AfterTargets="Build"` copy) rather than only at
  `bin/Debug/net9.0/`. `DI.cs` computes `DefaultEqDirectory` two levels
  above the executable, which lands at `bin/` on this SDK-style build.
  Getting the resource path right unlocked ~114 tests in one shot, all of
  which had been failing because `EQSpells` had no spells to look up.
- `BaseTestClass.cs` does `datetime.ToString("G")` followed by
  `DateTime.Parse(d)` (culture-sensitive, ICU on .NET 9 vs NLS on
  .NET Framework), and I had marked that as a plausible category (c)
  source. In practice zero tests failed on it under the invariant-ish
  behaviour of the default `en-US` culture on this box. That does not mean
  the risk is gone: a run under a culture whose `"G"` format is not
  round-trippable (e.g. `de-DE`) would very likely start dropping spells
  through the fast-forward loop in `LogParserExtention.Push`. Worth
  guarding once the native UI can set culture.
- Sixteen tests in `TriggerColorsTests.cs` initially failed with "these
  palette colors are too dark" / "too similar" because the stub
  `BrushConverter.ConvertFromString(name)` returned a default-constructed
  `SolidColorBrush` (RGB 0,0,0) regardless of the name. Every entry in the
  palette resolved to black, so all pairs were identical and all failed
  the >= 0.10 luminance floor. Fixed by giving the shim a real
  `NamedColors` lookup + `#RRGGBB`/`#AARRGGBB` hex parsing.

## Follow-ups for Milestone 2

Milestone 1 proves the parsers run on `net9.0` on macOS; Milestone 1.5
proves the upstream test suite (398/426) runs against them unedited.
Milestone 2 should be UI-shaped. Concrete next steps in priority order:

1. Native `SpellIcons` replacement. The stub now returns one `SpellIcon`
   per file so `EQSpells.BuildSpellInfo` populates `AllSpells`, but every
   returned `SpellIcon.Icon` is null — no `BitmapSource` payload. A native
   UI will need real bitmaps. Either read the `.tga` resources with a
   cross-platform decoder (e.g. Pfim + `SkiaSharp`) or ship pre-baked PNGs.
2. Real `IAppDispatcher` bound to whatever UI toolkit Milestone 2 picks
   (Avalonia is already spiked under `mac/spike/`).
3. `Paths.Combine` and `UIFileName.TryParse` divergence: **resolved**, see
   "Path normalisation" below. `Platform/MacPathNormalizer.cs` now provides
   the normalisation step this item called for.
4. The 12 handler-level SpellWindow tests
   (`SpellCounterTests`, `SpellMatchingTests`, `SpellViewModelTests`,
   `SpellWornOffOtherTests`, `SlainHandlerTests`, `CustomTimerHandlerTests`,
   `ItemBeginsToGlowTests`, `DeathTouchTriggerTests`, `DragonEffectTests`,
   `SpellAdaptiveGroupingTests`) all pass here against the real linked
   `SpellWindowViewModel`. Milestone 2 should keep them running as it
   swaps in the native UI shell for `SpellWindowViewModel`'s WPF
   consumers.

## Path normalisation

`Platform/MacPathNormalizer.cs`, tested by
`native/EQTool.Core.Tests/MacPathNormalizerTests.cs` (11 tests).

### The problem

The 28 remaining test failures all come from Windows-shaped path *data*, and
they made a second, quieter problem easy to miss. Both upstream helpers are
correct for native macOS input:

- `Paths.Combine("/Users/x/EQ/", "eqclient.ini")` returns
  `/Users/x/EQ/eqclient.ini`. On macOS `Path.DirectorySeparatorChar` and
  `Path.AltDirectorySeparatorChar` are both `/`, so the trims fire correctly.
- `UIFileName.TryParse("/Users/x/EQ/UI_Pigy_P1999Green.ini")` returns
  `PlayerName = "Pigy"`.

Given Windows input on macOS, neither throws. They corrupt silently:

- `Paths.Combine("C:\EQ\", "eqclient.ini")` returns `C:\EQ\/eqclient.ini`,
  because `\` is not a separator here so `TrimEnd` does nothing.
- `UIFileName.TryParse("C:\EQ\UI_Pigy_P1999Green.ini")` returns **true** with
  `PlayerName = "C:\EQ\UI_Pigy"`, because `Path.GetFileName` does not treat
  `\` as a separator on macOS.

The second is the dangerous one. A false success is harder to notice than an
exception.

This is not hypothetical. The Wine build stores Windows paths in
`settings.json` (`EqPath`, `EqLogPath`). Anyone moving from the Wine build to
the native client feeds exactly those strings in.

### Why the fix is not in `Paths.Combine`

`Paths.Combine` is correct on Windows and correct on macOS for macOS input.
Teaching it to treat `\` as a separator would change Windows behaviour, and
the file is upstream's — editing it costs merge cleanliness for every future
`git merge upstream/master`. Normalising at the native client's settings
boundary keeps the zero-upstream-edit property intact.

### Behaviour

`MacPathNormalizer.TryNormalize(path, winePrefix, out normalizedPath)`:

| Input | Result |
|---|---|
| `/Users/x/EQ/Logs` | unchanged, `true` |
| `Z:\Users\x\EQ` | `/Users/x/EQ` (Wine maps `Z:` to host root) |
| `C:\EQ\Logs` + prefix | `<prefix>/drive_c/EQ/Logs` |
| `c:\EQ\Logs` + prefix | same; drive letter is case-insensitive |
| `C:\` + prefix | `<prefix>/drive_c` |
| `C:\EQ\Logs`, no prefix | `false` — a drive letter cannot be resolved without one |
| `Logs\eqlog_Pigy.txt` | `Logs/eqlog_Pigy.txt` |
| null / whitespace | `false` |

It returns `bool` rather than a string specifically so an unresolvable drive
letter fails loudly. Returning a best-guess string would reintroduce the
silent-corruption failure mode this class exists to remove.

### Where it is called from

`EQTool.Avalonia`'s `Services/SettingsBootstrap.cs` runs both path settings
through it immediately after `EQToolSettingsLoad.Load()` and before the Autofac
container exists. That ordering is forced: `LogParser` starts its 100 ms poll
inside its own constructor, so by the time it is resolvable the paths must
already be native.

## Avalonia shell (Milestone 2)

`native/EQTool.Avalonia` is a normal desktop window that tails a real EverQuest
log file and draws the timers that come out of it. Run it with
`dotnet run --project native/EQTool.Avalonia`.

Scope is one window. No overlay, no click-through, no transparency, no
always-on-top — the Wine spike showed opaque overlay pixels swallow clicks on
both Windows and Wine anyway, so that is a later milestone with its own
questions to answer.

### Packages

| Package | Version |
|---|---|
| `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent` | 11.2.8 |
| `Avalonia.Diagnostics` (Debug only) | 11.2.8 |
| `Autofac` | 8.4.0 |

11.2.8 is the version `mac/spike/OverlaySpike` already proved on this machine.
`net9.0`, `RuntimeIdentifier` `osx-x64`, `ProjectReference` to `EQTool.Core`.
Not added to `EqTool.sln`.

### Nothing polls the log file

`LogParser` is self-driving. Its constructor creates a `System.Timers.Timer(100)`
and hooks `Poll`, which resolves the log location through `FindEq`, reads new
bytes with `FileReader.ReadNext`, and dispatches lines in chunks of 25 through
`IAppDispatcher`. Resolving it from the container is the whole of "start
tailing". The only clock the shell owns is a 100 ms `DispatcherTimer` that calls
`SpellWindowViewModel.UpdateSpells(dt_ms)`, which is what upstream's `UIRunner`
does at 1000 ms. The shorter interval costs nothing and makes the bars drain
smoothly instead of stepping.

### `AvaloniaAppDispatcher`

`Services/AvaloniaAppDispatcher.cs` implements `IAppDispatcher` against
`Dispatcher.UIThread`. `Poll` fires on a thread-pool thread and everything it
dispatches mutates `ObservableCollection`s that Avalonia is bound to, so those
updates have to be marshalled.

`DispatchUI` uses a blocking `Invoke`, not `Post`, matching upstream's
`Dispatcher.Invoke`: a batch of log lines is fully applied before `Poll`
continues. `TaskCanceledException` is caught and dropped, which is what a
dispatcher shutdown mid-batch looks like.

`EQTool.Core` is compiled with `TEST` defined, which strips `LogParser.MainRun`'s
own try/catch. Without a net, one bad log line would take the window down, so
dispatched work is wrapped and failures are written to stderr.

The stub `AppDispatcher` in `Compat/EqToolStubs.cs` is untouched. The container
binds `IAppDispatcher` to the Avalonia implementation instead.

### The `DispatcherTimer` shim needed a clock

This was the one genuine bug the milestone turned up, and it only shows on the
*second* firing of a trigger.

`TriggerTimerManager` builds a `System.Windows.Threading.DispatcherTimer` in its
constructor and prunes its `activeTimers` list from the Tick. The shim in
`Compat/WindowsShims.cs` never ticked, so nothing was ever pruned. On a second
match, `HandleTimerMatch` found the stale entry, took the `RestartTimer` branch,
and updated a `TimerViewModel` that `UpdateSpells` had already removed from the
spell list. `TryAdd` is only on the new-timer path, so the row never came back.
Silently.

The fix is an opt-in host:

```csharp
public interface IDispatcherTimerHost
{
    IDisposable Schedule(TimeSpan interval, Action onTick);
}

public class DispatcherTimer
{
    public static IDispatcherTimerHost Host { get; set; }
    // Start() subscribes through Host, if one is installed.
}
```

`AvaloniaDispatcherTimerHost.Install()` points it at
`Avalonia.Threading.DispatcherTimer`, so ticks land on the UI thread exactly as
WPF delivers them. With no host installed the shim is inert, so the test run is
byte-for-byte unaffected: a live background timer firing trigger output in the
middle of assertions is not something the suite should have to tolerate.

Screenshots `04` and `05` are the before and after of this: Dragon Roar expires
and vanishes, then comes back at 32s when the trigger fires again.

### Container

`Services/NativeContainer.cs` follows `EQtoolsTests/DI.cs`, the only non-WPF
composition upstream has. Parsers and handlers are discovered by reflection the
same way, so a new upstream parser is picked up by a rebuild. Two differences:

- The assembly comes from `typeof(LogParser).Assembly` rather than
  `AppDomain.CurrentDomain.GetAssemblies()`, because `EQTool.Core` may not be
  loaded yet at composition time.
- `FileReader` is registered `AsSelf().As<IFileReader>()` as one singleton. It
  carries the tail offset; `LogParser` takes the concrete type and handlers take
  the interface, and two instances would read the file from two positions.

`ITextToSpeach` binds to a no-op. Upstream's `TextToSpeach` needs
`System.Speech.Synthesis`, and spoken alerts are not part of this milestone.

### Settings and the log folder

`SettingsBootstrap.Load()` calls `EQToolSettingsLoad.Load()` (so `settings.json`
resolution, built-in trigger sync and all the rest behave as they do upstream),
then hands both path settings to `MacSettingsPathResolver`. The Wine prefix
comes from `WinePrefixLocator`: `PIGPARSE_WINEPREFIX`, then `WINEPREFIX`, then
`~/.wine-pigparse`, then `~/.wine`.

A path that cannot be resolved is left alone and reported, and the window shows
a plain-language band saying so, with the folder picker as the remedy. The three
messages are:

- the saved folder was written for Windows and cannot be found here
- no folder has been chosen yet
- the saved folder is gone

`settings.json` lives next to the executable, which upstream computes through
`Paths.InExecutableDirectory`. On this build that lands inside
`bin/Debug/net9.0/osx-x64/`, so it is wiped by a `clean`. A proper
`~/Library/Application Support` location belongs with app packaging.

### The window

`Theme/DesignTokens.axaml` holds every colour, spacing value, type size, radius
and font family the views use. Views carry no literal values, so the next screen
can only be consistent with this one.

The direction is an instrument panel rather than a document: near-black
blue-grey ground, hairline rules, amber as the signal colour and mint as the
"a character is being followed" colour. That keeps it in the same family as
`Example.png` (dark, compact, coloured bars that drain, name left and countdown
right) while fixing the thing that made the original hard to read: upstream
paints dark text directly on the coloured fill, so a half-drained bar leaves
half the label on a dark background. Here the fill sits at 30% alpha behind the
text, with the accent at full strength in a 3px spine down the left edge, so the
label reads the same at 100% and at 5%.

Countdowns are set in Menlo so the digits do not jitter as they tick, and turn
rose under ten seconds.

Rows cover `Timer`, `Spell` and `Roll`. Boats and counters share the same
collection upstream but neither is a countdown the player started: boats are
always present and cycle forever, counters have no duration. Neither belongs in
a list of what is running right now.

### Density pass

The first cut of this window was laid out like a desktop app: 30pt rows with 5pt
of vertical padding, a 4pt gap between them, and a 101pt header. That fits
fifteen timers in a 640pt window. A raid regularly runs more than fifteen, and
this window sits next to a live EverQuest client where scrolling to find a timer
mid-fight is not a real option.

The row pitch is now 19pt (18pt row, 1pt gap) and the header is 78pt, which fits
twenty-nine rows at the same window height. Screenshots `09` and `10` are the
before and after, both captured at 440x640 against the same firing sequence.

| Token | Was | Now |
|---|---|---|
| `SizeRowHeight` | 30 | 18 |
| row margin | literal `0,0,0,4` in the view | `GapRow` = `0,0,0,1` |
| `InsetRow` | `10,5,10,5` | `8,0,8,0` |
| `RadiusRow` | 3 | 2 |
| `TypeRow` (name) | 14 | 12 |
| `TypeCountdown` | 15 | 13 |
| `TypeMicro` (group label) | 10 | 9 |
| `TypeTitle` (brand) | 24 | 20 |
| `TypeHeading` (empty state) | 18 | 16 |
| `TypeBody` | 13 | 12 |
| `InsetHeader` | `16,12,16,12` | `14,8,14,8` |
| `InsetButton` | `12,6,12,6` | `10,4,10,4` |
| list padding | `InsetPanel` = 12 | `InsetList` = `10,4,10,4` |
| `SizeCountdownColumn` | 92 | 68 |

Rows no longer carry a vertical inset of their own. `SizeRowHeight` sets the
pitch and the contents centre inside it, so there is one number to change
instead of two that can disagree.

The type scale keeps its order: countdown at 13pt, name at 12pt, group label at
9pt. That order is the whole point. The countdown is what gets read under
pressure; the group is context the player already has.

`SizeCountdownColumn` came down to 68 because the widest string the formatter
can produce is `1h 2m 3s`, which is eight characters of 13pt Menlo. The 24pt it
gave back went to the name.

Two of the obvious levers had nothing left to give. The fill was already the row
background rather than a separate bar with its own vertical space, and the name
and countdown were already on one line.

Where I stopped short:

- Row height stopped at 18, not 17. A 12pt Avenir Next Condensed line box is
  about 16pt, so 18 leaves roughly a point of clearance top and bottom. 17 would
  have bought one more row and spent all the clearance to get it. The
  descenders in `Ramp_Swap` and `Sky_Ring_War` are the ones to check.
- `SizeSpineWidth` stayed at 3. At an 18pt pitch with a 1pt gap the accent
  spines nearly join into a continuous ribbon down the left edge, which is close
  to what `Example.png` does, and the spine is the row's only full-strength
  colour once the fill has drained.
- `RadiusRow` at 2 rather than 0. Square corners would have read as one solid
  block at this pitch; 2pt is enough to keep the rows separable without opening
  a visible gap.

Build and tests after the change:

```
dotnet build native/EQTool.Avalonia -warnaserror
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test native/EQTool.Core.Tests
Failed!  - Failed: 28, Passed: 422, Skipped: 0, Total: 450, Duration: 55 s
```

Unchanged from the baseline. Nothing under `EQTool.Core/` was touched; this is
`Theme/DesignTokens.axaml` and `Views/MainWindow.axaml` only.

### Verification

`dotnet build native/EQTool.Avalonia -warnaserror`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`dotnet test native/EQTool.Core.Tests`, run after the `WindowsShims` change:

```
Failed!  - Failed: 28, Passed: 416, Skipped: 0, Total: 444, Duration: 1 m 3 s
```

Unchanged from the Milestone 1.5 baseline. The 28 are the Windows-shaped path
test data documented above.

`git diff --stat HEAD -- EQTool EQToolShared EQToolApis EQtoolsTests EqTool.sln README.md LICENSE` is empty.

### Evidence

In `native/evidence/`, each inspected against the claim it supports and each
with a distinct MD5. Captured against
`~/.wine-pigparse/drive_c/EQ/Logs/eqlog_Sisytest_P1999Green.txt`, driven by
appending `You flee in terror.` (the built-in Dragon Roar trigger, 36 seconds)
and two `PigTimer` lines.

| File | Shows |
|---|---|
| `01-empty-state.png` | "Watching your log", character `Sisytest · Green`, log file name in the header |
| `02-timers-live.png` | Four rows three seconds after firing: Dragon Roar `You` 2s, Dragon Roar `Custom Timer` 32s, Sebilis_Pull 2m 56s, Ring_Roll 1m 26s |
| `03-timers-ten-seconds-later.png` | Same window ten seconds on: Dragon Roar 22s, Sebilis_Pull 2m 46s, Ring_Roll 1m 16s. The 2s row has expired and gone |
| `04-dragon-roar-expired.png` | Dragon Roar absent after its 36 seconds; the two PigTimers still running |
| `05-dragon-roar-refired.png` | Dragon Roar back at 32s after a second `You flee in terror.` |
| `06-log-folder-picker.png` | The native folder panel, titled "Choose your EverQuest Logs folder" |
| `07-log-folder-missing.png` | The notice band with an unreachable log folder, character line greyed to "No character detected yet" |
| `08-desktop.png` | The window in place on the desktop |
| `09-density-before.png` | The spacious layout at capacity: fifteen full rows in a 640pt window, a sixteenth clipped |
| `10-density-after.png` | The same window height after the density pass: twenty-nine full rows, a thirtieth clipped |
| `11-density-empty-state.png` | The empty state and the tightened header |
| `12-spell-icons.png` | Six spell-backed rows with their own icons above nineteen PigTimers on Feign Death's, twenty-five rows at the unchanged 18pt pitch |
| `13-icon-slot-fallback-probe.png` | The reserved empty slot, from the temporary probe described under "Nearly every row has an icon" |

### What surprised me

- The repeat-fire bug was invisible from a single trigger. Every screenshot of a
  first firing looked correct, and the app had been running for twenty minutes
  before a second firing exposed it. It would have shipped.
- Avalonia's Fluent `ProgressBar` carries a short fixed height and centres
  itself, so a templated fill renders as a 4px stripe floating in the middle of
  the row rather than filling it. `MinHeight="0"` plus
  `VerticalAlignment="Stretch"` fixes it, and the first evidence run caught it.
- `using Avalonia.Media;` cannot appear inside `namespace EQTool.Avalonia.*`:
  the compiler resolves `Avalonia` against the enclosing `EQTool.Avalonia` and
  fails. File-level using directives resolve from global and are fine, but any
  qualified reference in that namespace needs `global::`.
- `CustomTimer.CustomerTime` is `"  Custom Timer"` with two leading spaces, and
  `UpdateSpells` uses `!GroupName.StartsWith(" ")` to decide whether a timer row
  is hideable. Trigger timers are visible because of those spaces.

### Not done

- No grouping headers. Upstream groups rows by `GroupName` through a WPF
  `ListCollectionView`, which the shim is a no-op for. Rows here sit in
  insertion order with the group as a label on the row.
- No text to speech, no audio, no tray icon, no settings screen.
- `settings.json` still lives in the build output.

### Verifying UI changes: capture at native resolution

A downscaled full-screen screenshot cannot be used to judge type or spacing, and
trying to do so produced a wrong conclusion here.

`screencapture -x` on this machine yields 5120x2880. The app window is about
440pt wide out of 2560pt logical, so it occupies roughly a sixth of the frame.
Downscaling that to 1200px for the repository leaves 12pt row text about three
pixels tall. Everything looks cramped at that size, whether or not it is.

Judged from those downscaled images, the density pass appeared to have clipped
descenders, and `SizeRowHeight` was raised 18 to 20 to fix it. Re-inspecting a
native-resolution crop of the same capture showed descenders and underscores in
`Ramp_Swap`, `Sky_Ring_War`, `Xygoz` and `Hoshkar_Repop` rendering fully with
clearance at 18. There was nothing to fix; the change was reverted.

For anything involving glyph rendering, row pitch or spacing, crop the window
region out of the full-resolution capture and inspect that:

```bash
screencapture -x /tmp/shot.png
sips -c 1500 1000 --cropOffset 0 0 /tmp/shot.png --out /tmp/window-native.png
```

Downscale only for committing evidence, never before judging it.

## Spell icons

Built. `SpellIconSheets` supplies the pre-baked PNG sheets, `SpellIconService`
in the Avalonia layer decodes and crops them, and the timer rows draw the
result. The notes below record why the route was chosen; the section after them
covers what the build turned up.

The `SpellIcons` stub returns entries whose `Icon` is null, so nothing renders
an icon. Three routes were considered; the cheapest is clearly best, but it is
not worth landing on its own.

**Why upstream's decoder cannot simply be linked.** `EQTool/Services/Spells/SpellIcons.cs`
reads seven embedded `.tga` resources, decodes them with `TGASharpLib`, and
converts via `System.Drawing.Bitmap`. `TGASharpLib` is a source file in the
repo, not a package, so linking it costs nothing in dependencies — but it
carries 161 lines referencing `System.Drawing`: `PixelFormat` x51, `Color` x48,
`Bitmap` x21, plus `Rectangle` and `ImageLockMode`. The decode path
(`FromStream`) is separable from the `ToBitmap`/`ToBitmapFunc` tail, but
`ToBitmapFunc` does real pixel work through `LockBits`. Shimming that means
reproducing stride and pixel-format semantics exactly, which is easy to get
subtly wrong and hard to notice.

**The cheap route.** macOS decodes TGA natively. All seven files convert with
dimensions preserved:

```bash
sips -s format png EQTool/Spells/spells01.tga --out spells01.png
# 256x256 in, 256x256 out, 7 of 7 convert
```

So the icons can be pre-baked to PNG once, embedded in `EQTool.Core`, and
loaded by Avalonia's built-in PNG support. No new dependency, no `System.Drawing`
shim, no pixel-format risk. The cost is a generated artifact in the repository
that needs regenerating if upstream ever changes the sheets, which is rare.

**Why it was not built now.** Two halves are needed: making icons available,
and displaying them. The timer rows currently have no icon column, so building
the loader alone produces code nothing calls — the same trap `MacPathNormalizer`
briefly fell into. Note also that `SpellIcon.Icon` is typed as the shimmed
`BitmapSource`, which Avalonia cannot consume directly; the loader should expose
sheet bytes and sprite geometry and let the Avalonia layer build
`Avalonia.Media.Imaging.Bitmap` from them, rather than satisfying the WPF type.

Treat this as one piece of work: pre-bake, embed, expose sheet plus geometry,
add the icon column.

### Cropping without `CroppedBitmap`

Avalonia 11.2 does ship `Avalonia.Media.Imaging.CroppedBitmap`, so upstream's
markup could almost be transcribed. It is not a `Bitmap` though, only an
`IImage`, and it holds a live reference to the whole sheet and re-crops on every
draw. `SpellIconService` instead copies the pixels out once with
`Bitmap.CopyPixels` into a standalone 40x40 `Bitmap`, which is what makes the
cache worth having.

Two caches, both concurrent, because `LogParser` builds rows from a timer
thread: one sheet per index, one bitmap per (sheet, rect). The factories sit
behind `Lazy` so a 256x256 PNG is decoded at most once even under a race. With
the list rebuilt off a 100 ms tick and 25 or so visible rows, decoding per row
per tick would have dominated the frame.

### Nearly every row has an icon

I expected hand-made PigTimers to carry no icon, leaving the column empty on
most rows. That is wrong.
`EQTool/Services/Handlers/CustomTimerHandler.cs:66-76` deliberately lends custom
timers Feign Death's artwork, `SpellWindowViewModel.cs:636-640` does the same
for the API roll timers, and `RandomRollHandler.cs:26-36` uses Invisibility's.
So every countdown type the window shows resolves to a real icon.

`HasIcon` can still be false. `SpellExtensions.Map:37` only assigns `SpellIcon`
and `Rect` when the sheet number lands in 1..7, and leaves both at their
defaults otherwise. In the shipped P99 data it never does — the largest
`spell_icon` in `spells_us.txt` is 215, which maps to sheet 6 — so no log line
can reach the fallback. I checked it instead by temporarily forcing
`ResolveIcon` to return null for custom timers: the reserved slot keeps the
column's width and shows a 4px mark, and the names stay on the same left edge as
the rows that do have icons. The probe was then reverted.

Icons are drawn at 13px, not the 40px they are cut at, so a row carrying one is
exactly as tall as one that does not and the 18pt pitch is unchanged.

### Avalonia will not start while the display is asleep

Launching the app after the Mac has idled produces a crash inside Avalonia's
platform initialisation, before `Program.Main` reaches any application code:

```
Unhandled exception. System.InvalidOperationException: Avalonia.Native was not
able to start the RenderTimer. Native error code is: -6661
  at Avalonia.Native.AvaloniaNativeRenderTimer.add_Tick(Action`1 value)
  at Avalonia.Rendering.Composition.Server.ServerCompositor..ctor(...)
  at Avalonia.Native.AvaloniaNativePlatform.Initialize(...)
```

The stack points at rendering, so it reads like a graphics or build problem. It
is neither. The render timer needs an awake display.

Confirm before investigating anything else:

```bash
pmset -g powerstate IODisplayWrangler   # CurrentPowerState 0 means asleep
```

The decisive check is to run a second, unrelated Avalonia app — `mac/spike/OverlaySpike`
serves. If that fails identically, the cause is environmental rather than
anything in this project. It did, which is how this was ruled out as a
clean-build regression.

### Clean-build survival of the settings symlink: verified

`MacSettingsStoreTests` covers the logic against temporary directories,
including a simulated wipe of the build output. The end-to-end case was then
confirmed on a real clean, with the display awake:

1. Baseline: `~/Library/Application Support/PigParse/settings.json`, 74108
   bytes, `DefaultEqDirectory` of `C:\EQ`.
2. `dotnet clean` plus `rm -rf bin obj`. Symlinks under the build output: zero.
3. Rebuild and launch.
4. `bin/Debug/net9.0/osx-x64/settings.json` is a symlink to the canonical file
   again, and the canonical file is untouched at 74108 bytes with
   `DefaultEqDirectory` still `C:\EQ`.

The check reads the existing value rather than writing a probe into it. An
earlier attempt did write a marker into the real settings file, which then had
to be repaired; there is no reason to mutate user data to observe whether it
survived.

### Capturing a window that is behind other windows

`screencapture -R x,y,w,h` captures a region of the *screen*, so anything
stacked on top lands in the image instead of the window you wanted. Chasing the
Wine build's settings window that way produced two captures of an editor and a
terminal before the cause was obvious.

Capture by window ID instead, which ignores stacking:

```bash
# find the id (owner filter is the useful part; Wine reports owner "wine")
swift winid.swift        # CGWindowListCopyWindowInfo -> kCGWindowNumber
screencapture -x -o -l <windowId> /tmp/window.png
```

`CGWindowListCopyWindowInfo` also gives the window name, which is how the Wine
window was confirmed to be `SettingManagement` rather than a stray Wine desktop
window, and the layer, which distinguishes the tray icon (layer 25) from the
app window (layer 0).

## Console window

`Views/ConsoleWindow.axaml` over `ViewModels/ConsoleWindowViewModel.cs`, which
mirrors upstream's `ConsoleViewModel.ConsoleOutput`. It opens from the tray menu
and from the `Console` button in the timer window header, both through
`WindowManager`.

### The brush on a console line is not a brush

`ConsoleLine.Brush` is `System.Windows.Media.Brush` from
`EQTool.Core/Compat/WindowsShims.cs`. That type is a marker class: a `Freeze()`
that does nothing, a `CanFreeze` that is always true, and no colour anywhere on
it. The colour is on the `SolidColorBrush` subclass, as `Color { A, R, G, B }`.

Every value that reaches a console line comes from the shim's `Brushes` table,
which is entirely `SolidColorBrush`, so the mapping reads those four bytes and
builds an `ImmutableSolidColorBrush`. The results are cached on the shim
instance; `Brushes` members are singletons, so six entries cover everything
`DebugOutput` can produce. A brush that is not a `SolidColorBrush` has no
colour to read and takes `BrushTextPrimary` from the tokens rather than a
colour invented in the mapping.

In practice one colour shows up. `DebugOutput` picks white for
`Informational`, orange for `Warning`, green for `Success`, red for `Error`,
salmon and cyan for the two remote-message types — but every `OutputType.Spells`
call in the linked tree passes the default. The single exception is
`SpellCastOnOtherHandler.cs:58`, which logs `Could not match spell` as `Error`.

### Two channels, and only one of them can speak here

Nothing reaches the console unless `DebugOutput.LogMapping` or
`DebugOutput.LogSpells` is on, and both start false. Upstream puts them behind
two checkboxes in the settings window. This client has no settings window, so
opening the console turns both on and closing it puts them back. Leaving them
on for a window nobody is looking at is work done for no reader. The two
switches in the header are the same two flags.

`OutputType.Map` is only written by `Services/SignalrPlayerHub.cs`, which is not
linked — it needs SignalR. So the MAP channel is wired and toggles correctly but
has no writer in this build, and everything in the window arrives on SPELLS.

### The container was handing out a private console to every parser

The first run showed an empty window against a log that was definitely being
parsed. `NativeContainer` never registered `ConsoleViewModel` or `DebugOutput`,
so both fell through to `AnyConcreteTypeNotAlreadyRegisteredSource`, which
resolves per dependency. Each parser held its own `DebugOutput`, with its own
`LogSpells`, writing into its own `ConsoleViewModel`. The window switched a
flag on a seventh instance that nothing else could see.

Upstream's `DI.cs:71-72` registers both `SingleInstance`. So does
`NativeContainer` now. This is the same class of fault the `FileReader`
comment in that file warns about, and it fails silently in the same way.

### Following the tail without fighting the reader

The newest line is the point of the window, so it scrolls to the bottom as
lines arrive — but only while it is already there. `ScrollChanged` fires both
when the reader scrolls and when the content grows, and reading "not at the
bottom" from the second would switch following off on the first line to
arrive. Only a non-zero `OffsetDelta.Y` says anything about where the reader
wants to be, so that is the only thing that updates the flag. The scroll
itself is posted at `Background` priority, because a line that has not been
laid out yet has no extent to scroll to.

The 1000-line cap stays where upstream put it, on `ConsoleViewModel`. The
window mirrors `CollectionChanged` rather than rebuilding, so trimming the
front does not re-map a thousand brushes.

### Wrapped entries needed two fixes

`ScrollViewer` measures its content against the unpadded extent, so a
`Padding` on the scroller made every wrapped line overshoot the viewport and
lose its last few characters off the right edge — `casting Dazzle.` rendered as
`casting Dazzl`. The inset belongs on the list instead. Screenshot `17` is from
after the fix; the clipped capture was a working one and was not kept.

Entries also needed a gap. `SizeConsoleLineHeight` is 14 and a single entry
runs to three wrapped lines, so without `GapConsoleEntry` a three-line entry
reads as three entries.

### Log lines are prefixed with a full POSIX path

`DebugOutput.WriteLine` builds its prefix with
`filePath.Split('\\').Last()`, meaning to reduce a `CallerFilePath` to a bare
file name. On macOS `\` is not a separator, so nothing is stripped and every
console entry carries the absolute build-machine path. That is upstream's
string and this window prints what it is given, but it is why an entry wraps
over three lines here and one line on Windows.

### Evidence

| File | Shows |
|---|---|
| `17-console-live.png` | Seven entries from a real log tail: `YouBeginCastingParser` on Levitate, `SpellCastOnOtherParser` on `Jobob's feet leave the ground.`, `SpellCastOnOtherHandler` reporting `Skipped dt >= 2400 AND True`, `YouHaveFinishedMemorizingParser` on Aegolism, Dazzle, `YourSpellInterruptedParser`, `YouForgetParser` |
| `18-console-follows-tail.png` | Thirty-one entries with the view pinned to the newest |
| `19-console-holds-position.png` | The reader scrolled up; four more entries have arrived (35 lines in the header) and the view has not moved |

## The remaining integrations, and why they are not simply ported

Six upstream features are still unported. They are grouped here because the
decision on each is a judgement call rather than an engineering one, and three
of them should probably never be ported at all.

### Auto-update: must not be ported

`UpdateService` downloads a zip from `smasherprog/EqTool` releases, extracts it
over the running install and relaunches. Upstream ships Windows and Linux zips.
There is no macOS release, so on a native client this would download Windows
binaries and try to run them.

This is already guarded off for the Wine build with `#if MACOS` early returns,
for the related reason that it would otherwise overwrite the patched binary with
upstream's unpatched one. Porting it to the native client would be a defect, not
a feature. If the native client ever needs updating, it needs its own mechanism
against its own releases.

### Location sharing: works, but broadcasts more than it looks like

`SignalrPlayerHub` links cleanly. Its only WPF imports are
`System.Windows.Media` and `Media3D`, both already shimmed, and
`Microsoft.AspNetCore.SignalR.Client` has a net9.0 build.

The reason it is not wired up yet is behavioural. `SendMyLocationToOthers`
(`SignalrPlayerHub.cs:149`) fires on every `PlayerLocationEvent`, so on every
`/loc`, and sends character name, guild, server, zone and exact X/Y/Z to
pigparse.org. The `Sharing` preference travels *inside the payload* rather than
gating the send, so filtering happens on the server: the client transmits
whenever a server is known, regardless of the setting.

That is upstream's design and is presumably fine for people who opted into it
there. It is not something a port should switch on quietly, and it cannot be
meaningfully verified here anyway without a live hub and other players in zone.
It needs an explicit, defaulted-off opt-in before being wired.

### Discord login, inventory sync, UI file sync

`DiscordAuthService` (65 lines) and `InventoryWatcherService` (158 lines) carry
no WPF references at all; `UIFileSyncService` (417 lines) has one. So all three
are portable in the mechanical sense.

They upload character inventory and EverQuest UI `.ini` files to pigparse.org,
gated behind a Discord OAuth browser flow. None of it can be verified without a
real Discord account and the upstream API, and all of it moves user data off the
machine. Same conclusion as location sharing: an explicit opt-in, not a silent
port.

### P99 login middlemand

Binds a local socket and proxies login traffic. Portable in principle. Unverified
here because it needs a real P99 login attempt to exercise.

### Player and Server settings tabs

The Player tab is the inventory viewer and depends on Discord login, so it
follows that decision. The Server tab is 17 lines and can land whenever.

### The overlay interop: what is verified, and why the last step resists automation

The click-through path is covered in layers. Working outwards:

- `MacOSWindowInteropTests` checks the selectors exist on `NSWindow` using
  `class_getInstanceMethod`. This matters because `sel_registerName` *registers*
  an unknown selector rather than failing, so a misspelling yields a live
  selector that nothing responds to and a call that silently does nothing. One
  test registers a deliberate typo to show the check would catch it.
- `OverlayRenderTests` covers the parts that are ordinary Avalonia state: no
  window chrome, the drag handle staying hit-test visible while the content
  panels do not, the opacity clamp, and always-on-top.
- The guard tests confirm the interop declines rather than throwing when there
  is no `NSWindow` behind a window.

**The setters do take effect on a real NSWindow.** Verified directly, with no
display awake, by creating an off-screen `NSWindow` and reading the properties
back:

```
NSWindow created:                     ok
ignoresMouseEvents after set TRUE:    True
ignoresMouseEvents after set FALSE:   False
level after set 27:                   27
```

**That check cannot live in the test suite.** Creating the window from a test
crashes the host outright:

```
Terminating app due to uncaught exception 'NSInternalInconsistencyException',
reason: 'NSWindow should only be instantiated on the main thread!'
```

MSTest runs tests on worker threads, and the crash aborts the entire run rather
than failing one test, so it would take the other 84 with it. The manual probe
succeeded only because it ran on the process's main thread. Attempting this
again inside MSTest will reproduce the crash; it needs a main-thread test host,
which is not worth building for one assertion.

**What is genuinely unverified** is narrower than "click-through works": it is
whether the window server routes a real click to the window underneath. Every
layer beneath that is covered. Observing it needs a visible window and a real
click on an awake screen.

#### A dead end worth recording: windowNumberAtPoint:

`+[NSWindow windowNumberAtPoint:belowWindowWithWindowNumber:]` looks like it
should settle the click-routing question without a click, since it is the window
server's own hit-test rather than a simulation. Two overlapping windows, toggle
`ignoresMouseEvents` on the top one, see which number comes back.

It did not work here. The windows were created and reported `isVisible` true
with an alpha of 1.0, but the hit-test returned a window belonging to another
process in all three states and never responded to the toggle. A follow-up query
of `NSScreen` state then hung for two minutes and was killed.

This was run with the display asleep, which is the obvious suspect — no live
compositing context means nothing of ours is a hit target. That was not proven
though, because the screen query never returned. Recording it as inconclusive
rather than refuted: the approach may well work with a screen awake, and is
probably the first thing to retry.

### The 28 failing Core tests are not all the same thing

Twenty-seven of them are path-combine helpers fed Windows drive letters
(`C:\Everquest`, `C:\Program Files (x86)\Everquest`). .NET on macOS does not
treat a backslash as a directory separator, so the inputs never split. That is
test data describing a Windows machine, and nothing in the Mac build reaches it.

The twenty-eighth, `UIFileNameTests.ParsesFullPath`, is worth separating out. It
fails the same way, returning `C:\Everquest\UI_Pigy` where `Pigy` was wanted,
but it covers a parser that pulls a character name out of a UI file path rather
than a path helper.

`UIFileName` is compiled into the Mac build, because the core project takes all
of `EQToolShared` by wildcard. It is unreachable, though: its only caller
anywhere upstream is `UIFileSyncService`, which is not linked in, and the
Avalonia client never mentions it. Inert as things stand.

It stops being inert the moment UI file sync is wired up. Two of that service's
six calls hand `TryParse` a full path rather than a file name:

```
UIFileName.TryParse(e.FullPath, out var info)   // watcher event
UIFileName.TryParse(f, out var info)            // enumerated file
```

Fed a macOS path those are fine, since `GetFileName` splits on forward slashes.
Fed a Windows-shaped path they return the whole string as the character name.
Both are reachable that way: the EverQuest install sits behind Wine, and a
`settings.json` carried over from a Windows install stores Windows paths.

So this failure is a precondition on that feature, not a dismissible one. If UI
file sync is ever enabled, route those two calls through `MacPathNormalizer`
first and make `ParsesFullPath` pass.

### Correction: location sharing was live in the native client

An earlier note in this file described the four remaining integrations as "not
ported", location sharing among them. That was wrong, and the reasoning behind
it was wrong twice over.

The first mistake was checking whether a file was linked by searching the core
project for its name. Most of the upstream tree arrives through directory-wide
includes such as `EQTool\Services\Handlers\**\*.cs`, so individual file names do
not appear and the search returns nothing for files that are very much compiled
in. Every handler is present.

The second was assuming nothing constructed `PigParseApi` because nothing
mentioned it. `NativeContainer` registers `AnyConcreteTypeNotAlreadyRegistered`,
so Autofac builds whatever a resolved type asks for, and `RegisterCoreTypes`
registers every `BaseHandler` by scanning the assembly. `LogParser` then takes
an `IEnumerable<BaseHandler>` with the upstream comment
`//,_ this forces the creation of all handlers`.

The chain is short and entirely automatic:

```
LogParser -> SlainHandler -> PlayerTrackerService -> PigParseApi
```

`PlayerTrackerService` sets `UITimer.Enabled = true` in its own constructor on a
twenty second period. The elapsed handler returns early only if
`activePlayer.Player?.Server` is null; otherwise it calls `SendPlayerData`
against `/api/player/upsertplayers`. Nothing consults the `Sharing` setting,
which travels inside the payload rather than gating the send. The `App.httpclient`
in the compatibility shim was a plain `HttpClient`, so the requests were real.

So the native client uploaded character data once a character had been
identified from the log file. Twenty three separate handler chains reach
`PigParseApi`; `SpellWindowViewModel` and `TriggerTimerManager`, both registered
explicitly and resolved at startup, reach it too.

The fix is `EQTool.Core/Platform/PigParseNetworkGuard.cs`, a `DelegatingHandler`
on that client. Removing the types is not possible without editing upstream, and
the handlers around them are needed for spell and combat parsing. Blocking by
host does not work either, because the mob info wiki lookup lives on the same
host. So the guard allows `/api/item/wiki` and refuses every other path on
`pigparse.azurewebsites.net`, which also means a new upstream endpoint is denied
by default rather than admitted.

`NoNetworkReachabilityTests` covers both halves: that the reachability is still
real, so the guard stays load-bearing, and that the guard refuses the seven known
endpoints while letting the wiki through. One test reads the handler back off
`App.httpclient` by reflection, because a guard that works but is not installed
would pass everything else.

This is worth weighing when the sharing question is answered. The upstream
feature cannot simply be switched on: it needs a real gate on the send, not the
flag-inside-the-payload arrangement it ships with.

### Checking the network surface against the assembly, not the project file

Twice now a conclusion about what is compiled in has been wrong because it came
from searching `EQTool.Core.csproj` for a file name. Whole directories arrive
through includes like `EQTool\Services\Handlers\**\*.cs`, so individual names
never appear and the search reports absence for files that are present.

Loading the built assembly and asking it settles the question. Every type
holding an `HttpMessageInvoker` field:

```
EQTool.App  ::  httpclient (static)
```

One, and it is the guarded client in the compatibility shim. `LoggingService`,
`InventoryWatcherService` and `UIFileSyncService` each construct their own
`HttpClient` upstream, and none of the three is compiled into the Mac core.
`LoggingService` in particular is replaced by a no-op in
`Compat/EqToolStubs.cs`, which matters because `PlayerTrackerService` calls it
from the catch block around the request the guard now refuses. Upstream that
call posts the exception text to `/api/eqtool/exception`.

So `PigParseNetworkGuard` covers the native client completely. Prefer this check
over reading the project file.

### The Wine path does not have that guard

The Wine path runs the upstream binary, so none of the above applies to it. The
only upstream behaviour changed for the Mac configuration is the updater:
`#if MACOS` appears in `UpdateRunner.cs` and `UpdateService.cs`, and nowhere
else. `PlayerTrackerService` and `LoggingService` are untouched, so under Wine
the twenty second character upload and the exception posting both run.

That is not a regression. It is what the program does on Windows, and it is
upstream's decision. It is recorded because `mac/README.md` recommends the Wine
path, and recommending it without saying so is the part that would be wrong.
The disclosure now sits in that file above the install instructions.

Guarding it would be easy, since the `#if MACOS` mechanism is already in place
and already used for the updater. It has been left alone deliberately: the
updater guard prevents a broken action, whereas this would switch off a feature
somebody may want. That belongs with the sharing decision rather than ahead of
it.

### The guard costs item prices in the mob info window

The allow list admits `/api/item/wiki` and refuses everything else on the
service, and one of the things it refuses is `/api/item/postmultiple`. That is
the item price lookup. `ConHandler` calls the wiki first and then asks for
prices for whatever loot the mob is known to drop, so conning a mob still fills
the window in, but the price column stays empty.

The failure is quiet rather than damaging, which is worth knowing because it
could easily have been worse. `ConHandler` assigns the wiki result to the view
model *after* the price call and inside the same `try`, so a thrown exception
there would have left the whole window blank instead of merely unpriced.
`PigParseApi.GetData` checks for `HttpStatusCode.OK` before it reads the body,
so the refusal lands on the existing empty-result path and returns an empty
list. Two tests drive the real `PigParseApi` against the real `App.httpclient`
to pin that down, one for this call and one for the twenty second player upload.

Worth being clear that this block is stricter than the reason the guard exists.
`/api/item/postmultiple` sends item names and a server. It does not send a
character name, a guild, or a position. The argument for refusing it anyway is
that it still reports what you are looking at and when, from a machine whose
owner has not opted into talking to this service at all. The argument against is
that it costs a working feature for a request that cannot identify anyone.

That was decided one way here without asking, which is the wrong way round for a
trade of this kind. Adding the path to `AllowedPaths` in `PigParseNetworkGuard`
restores prices and changes nothing else.

### The allow list matches whole paths, not prefixes

The guard first compared paths with `StartsWith`, which was wrong in two ways.

A prefix admits anything that merely begins with the allowed path, so a future
`/api/item/wikiupload` would have been allowed without anyone noticing. It also
admits `/api/item/wiki%2F..%2Fplayer/upsertplayers`, because `AbsolutePath`
leaves `%2F` encoded: the string still starts with `/api/item/wiki`, and what
arrives at the service still carries `../player/upsertplayers`. The unencoded
form is the harmless one, since `Uri` resolves `../` before the guard sees it
and the result no longer matches.

Comparing the whole path closes both, and costs nothing: `WikiApi` posts to
exactly `/api/item/wiki` with no query string. Five refusal cases and the real
call are covered by tests.

### The one allowed endpoint does report your zone

Worth stating plainly, because the guard was first commented as though the
allowed call sent nothing of interest. It is a POST, not a lookup by URL, and
the body is `P99WikiLookup`, which is a name and a zone. So with the guard in
place the client still tells the service which zone the character is standing in
every time something is conned.

It stays allowed because refusing it does not degrade the mob info window, it
removes it: the wiki result is the window's content, and the price lookup that
the guard already refuses is only a column within it. What it does not carry is
a character name, a guild, or coordinates, which is the difference between this
and the twenty second upload the guard exists to stop.

If the answer on location sharing turns out to be no in the broad sense, rather
than no to the specific upload, this is the next thing to look at, and the cost
of refusing it is the whole mob info feature.

### Checking the whole network surface, not just the fields

The earlier check listed types holding an `HttpMessageInvoker` field and found
one. That was too narrow to support the conclusion drawn from it. A client
created inside a method rather than held in a field would not have appeared, and
neither would a raw socket, which would bypass the guard completely because it
never touches `HttpClient`.

The login middlemand is the specific worry there. It is a login proxy, so it
works at the socket level, and the guard could not see it at all.

Two checks settle it, both against the built assembly rather than the project
file. The first is what the metadata refers to:

```
System.Net    System.Net.Http    System.Net.Primitives
```

No `Socket`, `TcpClient`, `TcpListener`, `UdpClient`, `NetworkStream`,
`WebRequest`, `WebClient`, `HttpWebRequest` or `SslStream` anywhere.

The second is which of the seven upstream files that build a client or a socket
have their types present. `DiscordAuthService`, `InventoryWatcherService`,
`UIFileSyncService`, `LoginMiddlemand` and `SettingsPlayer` are all absent. Two
names do match, `App` and `LoggingService`, and both resolve to the replacements
in `Compat/EqToolStubs.cs` rather than the upstream versions: the field listing
shows `EQTool.App.httpclient` as the only invoker, so the `LoggingService` in
the build is the one with the empty `Log`.

Five tests hold this in place. One asserts that the guarded client is the only
holder of an `HttpMessageInvoker`, and four assert that the self-networking
services stay out of the build. Compiling any of them in would fail loudly
rather than quietly reopen the hole.

### The Wine disclosure, checked against the Wine binary

The warning in `mac/README.md` was first written by following the chain in
`NativeContainer`: `LogParser` takes every `BaseHandler`, `SlainHandler` takes a
`PlayerTrackerService`, and that constructor starts the timer. None of that is
evidence about the Wine binary, which runs the WPF application and its own
`EQTool/DI.cs`. The claim happened to be right, but it was not checked.

Upstream is more direct than the inference suggested:

```
EQTool/DI.cs:52          RegisterType<Services.PlayerTrackerService>().AsSelf().SingleInstance()
EQTool/App.xaml.cs:412   container.Resolve<PlayerTrackerService>()
```

It is registered by hand and resolved by hand during startup, so the twenty
second timer starts whether or not any handler would have pulled it in. `DI.cs`
also registers `AnyConcreteTypeNotAlreadyRegisteredSource` and scans for
`IEqLogParser` and `BaseHandler` in the same way, so the indirect route exists
as well.

The wording was also loose about when the posting begins. The elapsed handler
returns early while `activePlayer.Player?.Server` is null, so the timer runs from
launch and the requests start once the log has identified the server. The file
now says that instead of describing it as recognising the character.

### windowNumberAtPoint, retried with the screen awake

Retried once the display was actually on, since the first attempt was recorded
as inconclusive rather than refuted. It still does not work from a probe script,
and the reason is not the one guessed at the time.

What was established along the way:

- The call itself works through ctypes. Seven points returned five distinct
  results, with `0` for off-screen coordinates and real window numbers for
  points on screen. An earlier suspicion that `NSPoint` was being marshalled
  wrongly by value is wrong.
- The probe's own windows composite. They appear in
  `CGWindowListCopyWindowInfo`, alongside the couple of hundred others.
- `setActivationPolicy:` with `NSApplicationActivationPolicyRegular` returns
  false in an unbundled script.

So the windows exist and are drawn, but a point inside them returns some other
window, consistently, whatever the coordinate space. The likely reason is the
third item: a process that cannot become a regular application still gets its
windows composited, but they do not take part in hit testing. That is a
hypothesis rather than a demonstrated fact, and it is where this stopped.

Anyone retrying should do it from inside the Avalonia client, which is a regular
application and owns a real overlay window, instead of from a script. The
alternative remains a person clicking once.

### windowNumberAtPoint answers a different question, and the interop works

Run from a throwaway harness inside the repo that referenced the real Avalonia
project, so the windows were real Avalonia windows and the calls went through
the production `MacOSWindowInterop`. Two things came out of it.

The first is that the earlier failures were mostly a coordinate mistake. The API
wants Cocoa screen coordinates, with the origin at the bottom left, while
`Window.Position` is top left. Asking the `NSWindow` for its own `frame` removes
the guesswork: a window placed at `PixelPoint(380, 380)` on a 1440 tall display
reports `y=910`, and the centre of that frame does hit the right window. Note
that `frame` returns an `NSRect`, which needs `objc_msgSend_stret` on x86_64.

Two earlier explanations were wrong and are worth striking out. The point was
reaching the call correctly all along, and the activation policy was not the
problem either: the Avalonia harness reports policy `0`, a regular application,
and the result did not change.

The second, and the useful part, is the control that ran alongside it. Reading
`ignoresMouseEvents` back after each call to
`MacOSWindowInterop.SetIgnoresMouseEvents` gave false, then true, then false, in
step with the calls. That is the production interop working on a real overlay
window, which until now had only been shown on a synthetic `NSWindow` built by
hand in a script.

With that control in place the negative result means something:

```
RESULT_OFF_IS_TOP    : True     the hit test does find the window
RESULT_TOGGLE_CHANGED: False    toggling click-through does not change its answer
```

So `windowNumberAtPoint:belowWindowWithWindowNumber:` reports which window is
geometrically in front, not which window an event would be delivered to. It
cannot stand in for a click, and this line should be recorded as refuted rather
than retried again.

What is left is one step: whether the window server hands the click to whatever
is underneath. Everything before that is now covered, including the setter
working on the window the client actually shows.

### Click-through verified, and a correction to the note above

A real click does reach the window underneath the overlay. Measured, not
inferred, so the section above about `windowNumberAtPoint` needs correcting too.

The harness was the same throwaway project referencing the real Avalonia client:
two overlapping windows, both with a `PointerPressed` handler, `ignoresMouseEvents`
driven through the production `MacOSWindowInterop`, and the click posted with
`CGEventPost` to the HID tap. Resetting the z-order with `orderFront:` before
each trial matters, because clicking the lower window raises it and the next
trial then measures stacking instead of the flag.

| flag | front at point | click received by |
| --- | --- | --- |
| false | top | top |
| true | bottom | bottom |
| false | top | top |

The first row is the control. If posting were blocked or the handlers were not
wired, nothing would arrive in any row, which is exactly what happened on the
first attempt: the trial loop slept on the UI thread, so the run loop that had to
deliver the click was blocked. Posting the clicks from a background thread fixed
it. `AXIsProcessTrusted` is true here and a posted move does shift the cursor, so
permissions were never the obstacle.

Two earlier conclusions in this file were wrong and are withdrawn:

- `windowNumberAtPoint:belowWindowWithWindowNumber:` does track
  `ignoresMouseEvents`. The run that suggested otherwise was confounded by
  z-order. It is a usable check.
- The remaining gap was described as needing a person at the keyboard. It did
  not. A synthetic click through the HID tap answers it, provided the UI thread
  is left free to process the event.

This closes the click-through question for the native client. It says nothing
about the Wine path, where `WS_EX_TRANSPARENT` never reaches Cocoa and opaque
overlay pixels still swallow clicks.

### What the click-through result does and does not cover

The verification above used two plain windows created by the harness, not the
real `EventOverlayWindow`. That distinction is worth keeping straight.

What it establishes is the mechanism: `setIgnoresMouseEvents:`, driven through
the production `MacOSWindowInterop`, causes a real posted click to land on the
window underneath, and reverses. Both harness windows were opaque, which is the
harder case and the one that matters, since the Wine limitation is precisely
that opaque overlay pixels swallow clicks.

An attempt to repeat it against a real `EventOverlayWindow` instance did not
produce a usable result. The window builds and behaves as expected outside the
click itself: `Background` is `Transparent`, `DragHandle` reports
`IsHitTestVisible=True`, and `ignoresMouseEvents` read back false, true, false
in step with the calls. But no window received the posted click in any of the
three trials, including the control where the overlay's own drag handle should
have caught it. Since the control failed, nothing can be concluded from the
other two rows, and the run is recorded as inconclusive rather than as a pass.

The likely cause is the click coordinates rather than the overlay. They were
computed with `PointToScreen` on the drag handle, which reported a point twelve
pixels from the window origin, and if the window was not yet placed where the
call assumed then the clicks went somewhere else entirely. Worth checking
against the window's `NSWindow` frame instead, which is what fixed the same
class of mistake earlier.

Two other things that cost time here, both worth knowing before repeating this.
A bare `Application` subclass cannot construct the overlay, because the XAML
wants `FontDisplay` from `Theme/DesignTokens.axaml`; adding `FluentTheme` and
that dictionary is enough, and is preferable to configuring the real `App`,
whose startup builds a `MainWindow` and reads the live settings file. And the
classic desktop lifetime shuts the process down before any window is created
unless `ShutdownMode` is set to `OnExplicitShutdown`, which presents as the
program exiting silently with status zero.

### The real overlay does pass clicks through

The inconclusive run above was a coordinate mistake, and taking the point from
the `NSWindow` frame instead of `PointToScreen` fixed it, which is the same
correction that sorted out the hit test earlier.

The arithmetic is worth writing down. The frame comes back as
`x=400 y=590 w=640 h=450` for a window placed at `PixelPoint(400, 400)`. Cocoa
measures from the bottom, so the top edge is `590 + 450 = 1040`, the drag handle
centre sits twelve points below that at `1028`, and the CoreGraphics point that
`CGEventPost` wants is `1440 - 1028 = 412`. That lands twelve points in from the
window's top left corner, which is where the handle is.

With the point right, every row behaves:

| flag | front at point | click received by |
| --- | --- | --- |
| false | overlay | overlay |
| true | underneath | underneath |
| false | overlay | overlay |

The first row is the control, and it is the demanding one. The target was the
`DragHandle`, the only part of the overlay that is both opaque and left
hit-test visible so it can be dragged. A click there reaches the overlay when
click-through is off and reaches the window behind when it is on.

So this is settled for the native client, on the real window rather than a
stand-in: opaque overlay content does not swallow the click. That is the
difference from the Wine path, where `WS_EX_TRANSPARENT` never reaches Cocoa and
opaque pixels do swallow it, and it is the reason the native client exists.

### The click-through setting did nothing while the overlay was open

Found by checking a claim rather than the code. The click test drove
`MacOSWindowInterop.SetIgnoresMouseEvents` directly, which is not how the client
reaches it. Production goes through the settings checkbox, and that path was
broken.

`EventOverlayWindow` reads `OverlayClickThrough` once, in its `Opened` handler.
Nothing subscribed to changes and `Save()` only persists, so toggling the
checkbox against an overlay already on screen did nothing until it was reopened.

The comment at that call site describes the intended workflow: with click-through
on the overlay is purely visual, and it is "repositioned by turning the setting
off again". That is exactly the direction that failed. Turning it off left the
window still transparent to clicks, so the drag handle stayed unusable and the
overlay could not be moved, with the control appearing to do nothing.

`WindowManager` already tracks open windows by type, so the fix is a lookup and a
re-apply from the settings setter rather than any new plumbing.

Two things were needed to test it honestly. The first was an observable seam:
`WindowPreferences` now records the requested value per window in a
`ConditionalWeakTable`, because the interop leaves no readable trace off macOS
and none at all without an `NSWindow`, so there was otherwise no way to tell
whether a caller had reached it. The record is written on every platform, which
is what lets the test run on the Windows CI machine.

The second was checking the test actually fails without the fix. It does:
removing the one wiring line from the setter turns the run red on
`SettingsToggle_ReachesAnOpenOverlay` with "The toggle never reached the open
overlay", and putting it back turns it green. Worth doing, because the first
version of that test asserted only that the setting had changed and the window
was findable, neither of which would have noticed the bug.

The AppKit half was checked separately by driving the view model against a real
overlay and reading the window back:

```
initial                        ignoresMouseEvents = False
vm.OverlayClickThrough = true  ignoresMouseEvents = True
vm.OverlayClickThrough = false ignoresMouseEvents = False
vm.OverlayClickThrough = true  ignoresMouseEvents = True
```

### The window rows had the same fault, and one of them was never wired at all

Having found the click-through setting doing nothing to an open overlay, the
same shape was worth looking for elsewhere. `WindowPreferences.ApplyNow` is only
ever reached from `Attach`, which runs on `Opened`, so every row in the Windows
tab had it: changing always-on-top or dragging the opacity slider wrote to
settings and changed nothing on screen until the window was reopened.

Opacity is the one a user would notice first. The slider moves, and the window
it names sits there unchanged.

The Timers row was worse. `MainWindow` never called `Attach` at all, so its
always-on-top and opacity were written to `settings.json` and read by nothing,
on open or otherwise. Those two controls had never done anything.

Both are fixed the same way as the click-through setting, through
`WindowManager`, which already tracks open windows by type. The overlay row
re-applies with `asOverlay: true`; without it the overlay would be put back to a
normal window level and drop behind a Wine fullscreen window, which sits above
Avalonia's `Topmost`.

These needed no seam to test, unlike click-through: `Opacity` and `Topmost` are
ordinary Avalonia properties and can be read straight back on a headless window.

The mutation check earned its place here. Removing both re-apply calls turned
only two of the three new tests red, because
`AlwaysOnTop_ChangedWhileOpen_AppliesToTheWindow` had been written to set the
value true and then assert it was false after setting it back. Attaching had
already applied the stored value, so the window was false to begin with and the
assertion held whether or not anything was re-applied. Asserting the direction
that has to change fixes it, and all three now fail without the fix.

### Sweeping the settings window for controls that do nothing

Three separate faults of the same kind had turned up one at a time, so the
remaining ones were worth finding on purpose rather than by accident. The check
is mechanical: take every setting the settings view model writes, and look for
anything that reads it back.

The first pass was wrong and said five settings had no reader. It only searched
`native/`, and most of the code that consumes settings is upstream source linked
into the core, which physically lives under `EQTool/`. Searching there as well
accounts for `YouOnlySpells`, `ShowRandomRolls` and `ShowScoutRollTime`, all read
by `SpellWindowViewModel`.

An earlier note in this file claimed `SpellWindowViewModel` was replaced by a
shim in `Compat`. That is wrong. `Compat` holds only `EqToolStubs.cs`,
`Point3D.cs` and `WindowsShims.cs`; the view model is upstream's, it is compiled
in, and `NativeContainer` registers it as a singleton.

Two settings were genuinely dead.

`LogArchiveEnabled` had a checkbox and no service behind it.
`LogArchiveService` was never linked into the core, so ticking the box wrote a
value nothing read, on any code path. The service turned out to be portable as
written, needing only `System`, `System.IO`, `System.Timers` and
`EQTool.Models`, so it links without shims. It is registered and resolved in
`AppServices.Initialize` alongside the other constructors that start timers.

Building it unconditionally is safe because the work is gated on the setting,
which is a plain auto-property with no initialiser and so defaults to off. That
gate matters more than usual here: the service moves log files, and the tests
run against a temporary directory for the same reason. Breaking the gate turns
`TryArchiveLogs_WhenDisabled_LeavesFilesAlone` red, which is the check worth
having.

`LogArchiveSizeMB` is worth noting while this is fresh. It defaults to 100 and
has no control in the native settings window, so archiving can be switched on but
its threshold cannot be changed from here.

`FontSize` is the other dead one, and it is still dead. It is written by the
settings window and read by `App.xaml.cs`, `EventOverlay.xaml.cs` and
`MapViewModel.cs`, none of which are compiled into this build. Wiring it is not
mechanical: the windows take their sizes from the type scale in
`Theme/DesignTokens.axaml` rather than from an inherited window font size, so
setting `Window.FontSize` would change the few controls that do not name a token
and leave the rest alone. A control that changes some of the text is worse than
one that changes none, and choosing how a user font size should fold into a type
scale is a design decision rather than a repair.

### The rest of the settings surface is wired

Having found dead settings, the same question was worth asking of the controls
themselves rather than the values behind them. Two checks, both clean.

Every `Button` across the seven views resolves to something: a `Click`, a
`Command`, or a name the code-behind attaches a handler to. Every one of the
forty-two bindings in `SettingsWindow.axaml` resolves to a real property on the
view models or on the settings model.

`ShowRing8RollTime` is worth naming because it looked suspicious and is not. It
did not appear in the earlier list of settings the view model writes, but it is
read by `SpellWindowViewModel`, which is compiled in and registered.

Both first attempts at these checks were wrong, in the same direction: they
reported problems that were not there. Matching `<Button[^>]*` misses elements
written across several lines, which is most of them, and the property check
missed declarations whose brace sits on the next line. Each was caught by
disagreeing with something already known by hand, the button sweep flagging a
settings button verified minutes earlier as unwired. A sweep that contradicts a
checked fact is measuring badly, and its other rows are worth nothing until it
agrees.

### Finishing the archive threshold

`LogArchiveSizeMB` was listed as something to ask about, which was the wrong
call. Wiring the archive service up while leaving its threshold unreachable is
not a decision to put to anyone, it is half a feature: the checkbox turned
archiving on and the size stayed at whatever the default happened to be.

There is a slider for it now, from 10 to 500 in steps of 10, sitting under the
checkbox and disabled while archiving is off so the relationship between the two
is visible. The view model property reads straight off the settings object rather
than caching what was assigned, because `EQToolSettings` floors the value at 1
and so the value read back is not always the value set.

Three tests cover it. Two of them fail if the setter stops writing to settings,
which is the check that matters. The third pins the default at 100 and is not
sensitive to that mutation, which is correct for a test that only reads.

The first version of the markup did not close the `Grid` it opened, and the
element after it was the `StackPanel` end tag. Reading the region back caught it
before the build did.

### The font size setting works now

Calling this a design decision was wrong, the same way `LogArchiveSizeMB` was
wrong the round before. The tokens in `Theme/DesignTokens.axaml` are plain
numbers, and `TypeBody` and `TypeRow` are both 12, which is also the default of
the font size setting. So the slider's default already is the base of the scale,
and multiplying every token by `fontSize / 12` keeps the ordering and the gaps
exactly as the design file sets them out. That is arithmetic, not a second
opinion.

`TypeScale` does the arithmetic and writes the result into the application's
resources, where `DesignTokens.axaml` is merged. Sizes are rounded to a half
point, because text at an arbitrary fraction renders blurrier and it shows at
the small end.

The catch was the lookup. All fifty type references across the seven views used
`StaticResource`, which resolves once when a view loads, so rescaling the
resources would have changed nothing until each window was reopened. That is the
same fault fixed three times already this session, so they are `DynamicResource`
now. The 257 references to colours, spacing and radii were left alone: those do
not move.

It applies from the settings setter and again at startup before the first window
is constructed, so a stored size is in place when the window first lays out
rather than being applied afterwards and visibly jumping.

Six tests cover the arithmetic, including that the default reproduces the design
file exactly and that no rounding collapses two steps of the scale into each
other at any size the slider offers. A seventh checks the setting reaches the
live application resources, and fails if the setter stops applying it.

One part is not covered. `ZoneMapControl` draws its labels with sizes of 14, 11
and 9 written into `LabelFontSize`, rather than from the tokens, so map labels do
not follow this setting. The map draws to a canvas rather than composing
controls, so it needs its own handling.

### Map labels follow the font size too

The note above says they do not. That was true when it was written and is not
any more.

`ZoneMapControl` draws to a canvas, so it cannot pick a size up from a
`DynamicResource` the way the other views do. It calls `TypeScale.ScaleToCurrent`
instead, which reads the factor back out of the application resources rather than
out of settings. Reading it from the resources means anything drawn rather than
bound stays in step with whatever was last applied, without needing to know the
setting exists or subscribe to it.

Worth recording how close this came to shipping unguarded. The first test for it
asserted `TypeScale.ScaleToCurrent(14)` was 28, which is a fact about the service
and says nothing about whether the control calls it. Removing the call from
`LabelFontSize` left every test green. The test now calls
`ZoneMapControl.LabelFontSize` directly, which needed the method to be `internal`
rather than `private`, and the same mutation now fails it.

That is the third test this session that passed while proving nothing, and all
three were found the same way, by breaking the code on purpose and checking the
suite noticed. Writing the test is not the same as knowing it works.

### What upstream has that this build does not

The notes so far describe what works and say nothing about what is absent, which
invites the reader to assume parity. This is the gap, with the caveat that most
of it took three attempts to measure and some of it is still unmeasured.

Every upstream window has a counterpart here. `Console`, `DPSMeter`,
`EventOverlay`, `MobInfo`, `SpellWindow` and `SettingManagement` all map across,
and the map widget is `MapWindow`. Window parity is not feature parity though.

What is genuinely missing, established by checking the built assembly rather than
by searching text:

- The night vision graphics fix. It lives in `SettingsGeneral.xaml.cs`, which is
  WPF and not compiled.
- Reset triggers. Same file, plus `SettingsManagementViewModel`, also not
  compiled.
- The button that switches EverQuest's own logging on. Same file. `FindEq` is
  compiled, so finding the install still works; it is the button that is absent.
- The p99 login middlemand, which was already recorded as not compiled.

What I claimed and have withdrawn: raid mode detection, the friends list, and the
custom maps folder. Searching for "raid" and "friend" matched zone map data such
as `eastwastes_2.txt` and test fixtures such as `TestFight.txt`, so the counts
those searches produced described nothing at all. "cachemaps" appears nowhere in
the tree, upstream included. I do not know whether these three exist upstream
under other names, and the earlier numbers should not be trusted.

That is the fourth and fifth search this session to report something untrue. The
pattern is consistent enough to name: a quick grep across a tree that contains
game data, test fixtures and an uncompiled WPF application will answer almost any
question affirmatively, and none of those answers say anything about what the Mac
build does. The reliable question is whether a type is in the built assembly.

### EverQuest logging being off is now visible

This was sitting in a list of optional extras, which was the wrong place for it.
The client reads EverQuest's log file and nothing else. With EverQuest's own
logging switched off that file never grows, so every window stays empty and
there was no indication why: the settings window checked whether the log folder
was set, and never whether anything was being written into it.

`FindEq.TryCheckLoggingEnabled` was already compiled into this build. It simply
was not called.

The tri-state matters. It returns null when `eqclient.ini` cannot be read at all,
which is the ordinary state before an install has been located, so only an
explicit false raises the warning. Writing `!TryCheckLoggingEnabled(dir)` instead
would show it to everyone on first run, and it looks like the tidier expression,
which is why the comparison carries a comment.

Six tests, all against a temporary directory rather than a real install. They
cover the file saying true and false, the file being absent, no directory set at
all, hand-edited spacing and casing, and rechecking when the directory changes.
Removing the recheck from the setter fails that last one.

Enabling logging from here is a separate question. Upstream writes `Log=TRUE`
into `eqclient.ini` from `SettingsGeneral.xaml.cs`, which is not compiled, and
writing to a file EverQuest may have open is a different kind of change from
reading one. The warning names both routes and leaves the choice with the reader.

A process note, since it cost real work. Mutation testing by editing a file and
reverting with `git checkout --` destroys anything uncommitted in that file. The
first mutation was fine; reverting it threw away the entire feature, which was
still unstaged, and the next mutation then failed to find its anchor. Commit
first, mutate second.

### User trigger patterns ran with no time limit

Found while asking whether "Reset Triggers" is a convenience or a way out of a
broken state. That question led to `Trigger.cs`, where the comment above the
regex compile says the match timeout is the process-wide default set in `App`'s
static constructor, and that user-authored patterns are the ones most likely to
backtrack catastrophically.

`App` in this build is the stub in `Compat/EqToolStubs.cs`. It never carried that
constructor over, so nothing set the key and every pattern ran unbounded.

The consequence is not a crash. A pattern that backtracks catastrophically does
not fail, it runs, and it runs on the log parsing thread, so the client stops
updating and stays stopped. Restarting does not clear it either, because the log
line that triggered it is still there to be matched again. Compilation was
already guarded, so a malformed pattern was handled; an expensive one was not.

`RegexSafety.Install` sets it now, as the first statement in `Main` and from the
stub's static constructor. Ordering is the whole property: `Regex` reads the key
once, the first time the type is used anywhere in the process, and caches it
forever, so a call arranged later during startup would do nothing. 25ms is
upstream's value and its reasoning holds, the deadline being compared against
`Environment.TickCount`, which only moves every 15.6ms or so.

What the tests show is narrower than the fix. Since `Regex` caches the default on
first use, and the test host has certainly built one before any test runs, no
test can install the default and then watch it take effect. They check the value
is put in place, that a second call leaves an existing one alone, that 25ms does
abort `^(a+)+$` against forty characters, and that it is still generous enough
for an ordinary log line. The catastrophic case runs on a worker with a ten
second cap so that a missing bound fails the test rather than hanging the run.

### Auditing the stubs deliberately

Four defects so far had come out of `Compat/`, each found by accident. The
regex timeout was the clearest: upstream put it in `App`'s static constructor,
the stub replacing `App` did not carry it, and nothing failed. That is the shape
to look for, so this went through every stub asking what the original did that
the replacement does not, and whether compiled code still reaches it.

Thirty-four types. Most of `WindowsShims.cs` stands in for WPF types such as
`Brush`, `Rect` and `Visibility`, where there is no upstream behaviour to lose.
The ones worth examining are in `EqToolStubs.cs`.

Two findings.

`BinarySerializer` throws from both methods. Upstream uses `BinaryFormatter`,
which .NET 9 removed, which is why it was stubbed. Every call site catches:
`MapLoad` falls back to parsing the map file, `ParseSpells_spells_us` falls back
to parsing the spell file. So maps and spells are correct, and the binary caches
those two paths were written to use never work. The cost is startup time on every
run rather than once. Restoring it means choosing a different format, since the
old one cannot be read or written on .NET 9 at all.

`ForegroundWindowHelper.IsEqGameFocused` returns false, always, and the call site
reads:

```
// Only warn when eqgame is NOT the focused window.
if (ForegroundWindowHelper.IsEqGameFocused()) return;
```

So the early return never happens and the attacked-while-away alert fires while
the player is looking at the game, which is exactly when it should stay quiet.
Both switches behind it default to false, so nobody sees this without turning the
alert on first, and anyone who does turn it on gets an alert in every fight.

It is left unimplemented on purpose. Answering it means knowing whether the
EverQuest window is frontmost, and under Wine that is a Wine-hosted window on a
machine with no EverQuest installed to check against. Matching on a guessed
bundle identifier is the kind of unverified assumption that has already cost this
port several rounds.

The rest are sound. `LoggingService` does nothing deliberately, since the real one
posts exception text containing the account name. `SpellIcons` returns
placeholders because the native client draws icons through `SpellIconService`
instead. `SettingsWindowViewModel.GroupLeaderName` is written by
`GroupLeaderHandler` and read by nothing here, so the group leader is tracked and
never displayed, which is a missing readout rather than wrong behaviour. The
interfaces and enums carry no behaviour.

### The missing binary caches cost almost nothing

The previous note left this as a decision about which serialisation format to
pick. That was the wrong framing twice over. The cache format is internal, so it
is not a decision for anyone outside the code, and more importantly nobody had
measured what the cache was worth.

Measured on this machine, parsing from source with no cache at all:

```
spells      191 ms once at startup, for 4780 spells
fearplane    47 ms   1113 lines,  23 labels
kaesora      16 ms   1630 lines,  23 labels
sebilis      13 ms   2679 lines,  46 labels
kaladimb      4 ms   1351 lines,  30 labels
eastwastes   20 ms   1919 lines,  44 labels
```

The first map includes warm-up; the rest sit between 4 and 20 ms. So the cache
was saving about a fifth of a second at launch and a few milliseconds per zone
change.

That is not worth reintroducing. Writing it back means choosing a format,
serialising two model graphs, and then owning cache invalidation, which is a
category of bug that pays for itself only when the thing being cached is slow.
This one is not, so `BinarySerializer` stays as a stub that throws into the
existing catch blocks, and the parse path that already runs stays the only path.

Worth keeping the numbers rather than the conclusion alone. If the map format
grows or spell parsing gets heavier, the trade changes, and the next person
should re-measure rather than trust this paragraph.

### The volume setting did nothing for spoken triggers

The stub audit covered `Compat/` and stopped there, which left out the other half
of the same risk. `Platform/` holds replacements written for macOS rather than
stubs, and a replacement can drop behaviour just as quietly as a stub can.

Comparing `MacTextToSpeach` against upstream's `TextToSpeach` shows it. Upstream
reads two settings before speaking:

```
var voice  = eQToolSettings.SelectedVoice;
var volume = eQToolSettings.GlobalAudioVolume ?? 100;
```

Mine read the voice and never looked at the volume. `MacAudioService` does read
it, clamps it, and declines to play at zero, so the slider worked for sound file
alerts and did nothing at all for spoken ones. Two alert types, one control, and
it only moved one of them.

`say` has no volume flag. The only route is an inline speech command, which was
worth confirming rather than trusting: rendering the same phrase to a file at
`[[volm 1.0]]` and `[[volm 0.1]]` gives peak amplitudes of 18206 and 1514, so it
does what the documentation claims.

The command is added only when the volume is actually below full. `[[volm 1.0]]`
is what `say` already does, so adding it always would rewrite every phrase for no
effect, and the default path stays exactly as it was. All five existing tests
pass unchanged, which is the evidence for that rather than an intention.

One test sets the thread culture to `de-DE` before speaking. Formatted without
`InvariantCulture` the command becomes `[[volm 0,35]]`, which `say` cannot parse
and reads out loud instead of obeying, so the phrase would be prefixed with
spoken punctuation.

### The settings redirect deleted a file it promised to move

Carrying the `Platform/` audit past the two files with direct upstream
counterparts. `MacSettingsStore` looked like it might be one, since upstream's
`EQToolSettingsLoad` does more than read JSON: it retries three times, back-fills
null player lists, fills in missing spell class defaults, migrates enums, and
runs `SyncBuiltInTriggers`, which is what the promise that built-in triggers pick
up fixes on update rests on.

None of that is lost. `SettingsBootstrap` builds the upstream loader and calls
`Load()`, so the whole chain runs. `MacSettingsStore` only decides where the file
lives, redirecting the executable directory path at
`~/Library/Application Support/PigParse` through a symlink.

Its own logic had a hole though. `MigrateExistingSettings` carries the comment
"Move it rather than deleting it, but never overwrite a canonical file that
already exists", and it returned early when the canonical file existed. Control
then reached the caller's `File.Delete(linkPath)`, which is there to clear the
path for the symlink. So in the one case where both are real files, the comment
promised a move and the code performed a delete.

The live settings were never at risk, since the canonical file is the one in use
and is untouched. What was thrown away was the other real configuration, which is
the one somebody would be looking for.

It is moved to `settings.json.superseded` beside the canonical file now. The
existing test for this case asserted only that the canonical file kept its
contents and said nothing about the other, which is why the delete sat there
unnoticed; the new test asserts the superseded content survives.

### Redirecting a pipe nobody reads

Hunting the category the settings redirect fell into: a comment stating an
intent the code below does not carry out. `ProcessLauncher` had one.

Its comment says nothing there may block, and that each process disposes itself
on exit rather than accumulating handles across a session. The code redirects
both standard output and standard error, and reads neither.

A child that fills the pipe buffer blocks on write and stays blocked. It never
exits, so `Exited` never fires, so the handle is never released and the stuck
child stays too. Both promises fail together, and they fail in the same case.

Measured rather than reasoned about. The same child writing 200KB:

```
redirect=true    exited within 4s = false
redirect=false   exited within 4s = true
```

For `say` and `afplay` the output is a few bytes, so this was latent rather than
biting. It is still worth removing, because the redirect had no upside at all:
nothing read the pipes, so the only thing it contributed was the condition for
the deadlock.

Both pipes are drained now with empty handlers, which keeps the child's output
off the terminal without the risk. `Start` is also wrapped, since a missing
executable throws and these calls come from the log parse thread during combat,
where an escaping exception stops parsing.

The tests launch a child that writes past the buffer and touch a sentinel file
afterwards, so completion is observable from outside the launcher. The waits are
bounded, so a regression fails the test rather than hanging the suite.

### What the client has actually been tested against

Worth writing down plainly, because every "verified" in this file should be read
against it.

The configured EverQuest directory is a Wine-shaped prefix, and the settings
point at it:

```
DefaultEqDirectory = ~/.wine-pigparse/drive_c/EQ
EqLogDirectory     = ~/.wine-pigparse/drive_c/EQ/Logs
```

Both exist. What they contain is `eqclient.ini` carrying `Log=TRUE`,
`license.txt`, `spells_us.txt`, and a `Logs` folder holding one file:
`eqlog_Sisytest_P1999Green.txt`.

There is no `eqgame.exe`. This is a scaffold holding the minimum set of files the
client needs to run, and the log in it is named after a test character invented
for this work. Every feature described as working in this file works against
that.

Two things follow.

The Wine assumption behind the install guide, the path normaliser and the
`#if MACOS` updater guard is correct. The paths the client reads are Wine-shaped
and it is pointed at them deliberately.

`IsEqGameFocused` is blocked, but not for the reason first recorded. The original
note said the frontmost window could not be detected without an install. That was
wrong: `NSWorkspace.frontmostApplication` answers with a process id, a localised
name and a bundle identifier, and was never tried before being called impossible.
It is blocked because no EverQuest process exists here to be frontmost, so no
detection rule can be checked against the thing it is meant to match.

A native `EverQuest.app` does run on this machine, from a separate project
outside this repository. It is not what the client reads from and it does not
match upstream's rule, which compares the process name against `eqgame`.

### Correcting the caveat: what is and is not validated

Every summary of this work has ended with the same line, that the client has only
been tested against log lines written by hand for the purpose. That is true of
one layer and wrong about another, and the distinction matters.

`EQTool.Core.Tests` links 41 of upstream's 42 test files. They contribute 375
test methods against 85 of mine. `DamageParserTests`, `SpellMatchingTests`,
`ZoneParsingTests`, `PetTests`, `SlainHandlerTests`, `FTETests`,
`RandomRollTests`, `AuctionParsingTests` and the rest all run here, against the
same linked parsers the client uses.

Their fixtures are message bodies, since the parsers take the message and the
timestamp separately:

```
"Vebanab slices a willowisp for 56 points of damage."
"Ratman Rager was hit by non-melee for 45 points of damage."
```

Names like those are not what someone writing a parser test from nothing would
invent. They read as lines lifted from real logs, which is inference rather than
proof, but it is strong.

So the parsing layer is checked against expectations written by people running
this against a real game. Damage, spells, zone changes, pets, rolls, first to
engage, slain messages, auctions, comms and triggers all sit behind those 375
tests, and they pass.

What remains unvalidated is everything around the parsers. Whether the log file
is found and tailed correctly, whether timers fire and expire on screen, whether
the overlay draws in the right place over a running game, whether audio and
speech actually reach the player, and every Mac-specific piece written for this
port. That layer rests on my 85 tests and a scaffold directory holding one log
file named after a test character.

The honest form of the caveat is therefore narrower than the one used so far:
the parsing is borrowed and tested, the integration and the macOS layer are not.

### Reading the log twice

The previous note split the work into a parsing layer covered by upstream's 375
tests and an integration layer covered by nothing. The most important thing in
that second layer is the one the whole client sits on: a line arrives in the log
file and reaches a handler.

`FileReader` keeps the offset it stopped at, so what it returns is what is new
since the last call. Upstream's `FileReaderTests` reads once. Reading once is the
only thing the running client never does; it polls every 100ms and reads again.

If that offset stopped advancing, each poll would hand back the same lines and
every trigger in them would fire ten times a second. That failure presents as the
parser being wrong, or a trigger being badly written, rather than as the reader
losing its place, so it is worth a test that pins the behaviour directly.

Five cover it: a second read after an append returns only the appended line, an
unchanged file returns nothing, five successive appends each return exactly one
line, a rotated file that shrinks underneath the reader is still followed, and
switching to another character's log follows the new file.

`GetLogFileLocation` was checked at the same time and is sound on macOS. Its path
separator defaults to a backslash and only becomes a forward slash when
`EqBaseLocation` contains one, which looked like a problem here. It is not: the
separator is only used on the fallback branch, that branch is guarded by
`EqBaseLocation` being non-empty, and any non-empty path on this platform
contains a forward slash. When the log directory is set and holds the file, the
function returns before the separator is read at all.

These run against a temporary directory, not the configured Wine prefix, so no
real log is touched.

### Which of those five actually catch a stalled offset

Worth recording, because the answer is one of them and that was not the
expectation.

Removing `LastLogReadOffset = stream.Position` from inside the read loop is the
stalled-offset failure in its purest form. Run against it, four of the five tests
still pass. Only `ReadNext_AcrossManyPolls_NeverRepeatsALine` fails.

The reason is structural rather than accidental. On the first read of a path the
offset is set to the file's length *before* the loop runs:

```
if (!LastLogReadOffset.HasValue || ...)
{
    newPlayerEventEmitted = true;
    LastLogReadOffset = fileinfo.Length;
}
```

So after one read the offset is already correct, whether or not the loop
maintains it. A test that appends once and reads once cannot tell the difference.
The staleness only compounds on the third read and later, which is why polling
repeatedly is the only shape that exposes it, and why the client, polling ten
times a second, would have shown it immediately.

The other four are not thereby useless. Returning only the new line, returning
nothing from an unchanged file, following a rotated file and following a
character switch are each worth pinning and would catch other regressions. They
are simply blind to this one, and a mutation run is the only way that becomes
visible.

### The trigger timer lifecycle had no test at all

Last note closed by saying timers firing and expiring need a real client in front
of a real player. That was wrong, and it was the fourth time this session that
something was called untestable without being tried.

`TriggerTimerManager` appears in no test file, upstream's or mine. Upstream's
`TriggerTests` cover loading triggers, folder layout, built-in defaults and
duplicate merging, none of which touch the timer lifecycle.

That is the code a real fault already lived in. The manager prunes its list of
running timers from `Tick`, `Tick` comes from the `DispatcherTimer` shim, and the
shim does nothing until a host is installed. Without one the list never shrank, a
second match on the same timer found the stale entry, took the restart branch,
and updated a view model that had already left the spell list, so the row never
returned. Installing a host fixed it and nothing locked it.

It needs neither a game nor a screen. `DispatcherTimer.Host` is an interface
behind a settable static, so a test host can capture the scheduled callback and
fire it on demand.

The configuration matters more than it looks. `RestartBehavior` defaults to
`StartNewTimer`, which adds a row whether or not the expired one was pruned, so a
test using the default passes with the fault present. Only `RestartTimer` reaches
the branch that mutates an existing entry, which is where this breaks.

Four tests: a match adds a row, resolving the manager schedules a tick at all, a
tick before expiry leaves a running timer alone, and after expiry plus a tick the
same trigger starts a fresh timer rather than quietly updating a row that is gone.

### No window remembered where it was

`WindowState` has carried a `WindowRect` all along and nothing in this client
ever read or wrote it. Upstream saves and restores it from
`EQTool/UI/BaseSaveStateWindow.cs`, a WPF base class every upstream window
derives from, which uses `GetWindowRect` and is not part of this build. Its
screen check, `WindowBounds.isPointVisibleOnAScreen`, is built on
`System.Windows.Forms.Screen` and is equally unavailable.

So every window opened at its default, every launch. The overlay supports
dragging and forgot where it had been dragged to, which for a window whose whole
job is to sit in one place over the game is the worst version of this.

`WindowPreferences` now captures position and size when a window closes and
applies them when it opens. The screen check is Avalonia's `Screens`, and only
the corner is tested: `Position` is in physical pixels while `Width` and `Height`
are device independent, so adding them together would be wrong on a scaled
display. What matters is that the window lands somewhere reachable, which is the
same thing upstream guards against.

Saving to disk hangs off `WindowPreferences.Persist`, a static the shell installs
during `AppServices.Initialize` and nothing else sets. That mirrors
`DispatcherTimer.Host`, which already works this way here, and it means a window
built in a test records its geometry in memory and writes nothing.

Two things worth recording about the tests rather than the code.

`Restore` was written and never called. It compiled, it read correctly, and it
was dead until the call was added to `ApplyNow` — the same shape as the faults
this session has been finding elsewhere, produced fresh.

The first six tests covered the guard and not the thing it guards. There was a
case for a rect far off every screen not being restored, and no case for a rect
on a screen being restored, so a `Restore` that never applied anything would have
passed all six. The headless backend reports one screen at 0,0,1920,1280, which
is what makes the positive case meaningful: 300,200 is inside it and is applied.

### Windows did not come back after a restart

`WindowState` carries three fields. The last note fixed `WindowRect`; the other
two were sitting in the same struct, equally unread.

`Closed` is written by upstream's `BaseSaveStateWindow` when a window opens and
closes, and read in `App.xaml.cs`, which reopens each window that was still open
when the client last exited. Nothing here wrote it and nothing read it, so every
launch produced one window and the rest had to be reopened by hand. Together with
the geometry fault, they would not have come back in the right place either.

`WindowPreferences` now clears `Closed` when a window opens and sets it when the
window closes, alongside capturing the geometry. `App` reopens the map, DPS, mob
info, console, overlay and settings windows, each guarded on its own so a window
that throws on construction cannot take startup with it. The timers window is the
main window and is already open.

One part of this is a judgement rather than a repair, and it is worth naming.
Upstream's fresh-install defaults set `Closed = false` on five window states, so
porting the check literally would open five windows at once for somebody who has
never run the client. That reads as wrong: on a first run there was no previous
session, so nothing was open. `ShouldReopen` therefore also requires a stored
rect, on the grounds that a window genuinely open at exit went through `Capture`
on its way out and a window never opened has no geometry at all. It is a proxy,
it is mine rather than upstream's, and it can be dropped by deleting one
condition if the upstream behaviour is preferred.

`WindowState.State`, which carries maximised and minimised, is still unread. It
is left that way deliberately: none of these windows can currently be maximised
in a way worth restoring, and inventing that is a second judgement on top of the
first.

### Sweeping the settings model rather than one field at a time

Six faults in this file were dead settings found one by one: a log archive
checkbox with no service, a font size nothing read, an overlay click-through that
only applied on open, a window position never saved, a closed flag never
consulted. Each was found by tripping over it. `WindowState` made the pattern
obvious, since it holds three fields and two of them were dead while only the
first was noticed.

So the whole model was walked instead. Thirty-three public properties on
`EQToolSettings`, each checked for a reader in code that is actually compiled
here.

Eleven are read by linked upstream code and were already working: the player
list, triggers and trigger folders, the three roll display flags, the spells
filter, the selected voice, the audio volume, and the two EverQuest directories.
Six were dead and are fixed. Six are the per-window state objects, now handled.
`WindowState.State` is knowingly unread.

Five are left: `DiscordApiToken`, `DiscordId`, `DiscordUsername`, `SyncUIFiles`
and `LoginMiddleMand`. Every reader of those lives in a file this build does not
compile, which is the expected shape for features that were never ported, and the
settings window has no control bound to any of them. Nothing offers a switch that
does nothing, which is the part that mattered.

The sweep found nothing new, and that is the point of recording it. This class of
fault is now exhausted rather than merely quiet, and the next dead setting would
have to be introduced rather than discovered.

One measurement note. The first pass listed readers by file basename, which put
`SettingsWindowViewModel.cs` in the results without saying whether that meant
upstream's or the native one. The earlier sweep of this area had only looked at
writes, so a read-only binding to an unported feature would have escaped both.
Re-running against `native/` alone is what settled it.

### The second settings container, swept the same way

`EQToolSettings.Players` holds `PlayerInfo`, which carries fifty-one public
properties, most of them per-character alert toggles: first to engage, root
warnings, death loops, bard counts, complete heal chains, Zlandicar. That is the
same shape as the container that produced six faults, and it had never been
looked at as a whole.

Fourteen of the fifty-one have no reader in code this build compiles. None of the
fourteen is referenced anywhere under `native/`: no binding, no code path, no
control. They belong to upstream features that were not ported, which is the
same benign category as the Discord fields in the previous sweep.

Worth separating the two measurements, because one of them is weak. Deciding
which files are compiled was done with a hand-maintained exclusion list, so the
count of fourteen is approximate and should not be quoted as though it were
exact. The question that matters, whether the native client offers a control
backed by nothing, was answered by searching `native/` directly with no list and
no heuristic, and that answer holds whatever the first number really is.

Both settings containers are now exhausted. Six dead settings were found and
fixed by tripping over them one at a time; two deliberate sweeps since have found
nothing further. The next one would have to be introduced rather than discovered.

### The settings window was on the reopen list and never attached

Found by checking my own work from two rounds earlier rather than by another
sweep. `App.ReopenLastSession` names six windows. Only a window that calls
`WindowPreferences.Attach` has its state written, and `ShouldReopen` needs a
stored rect to tell "was open" from "never seen".

`SettingsWindow` was the one view that never attached. So its line on the reopen
list could not fire under any circumstances, and the window did not remember its
position or size either, unlike the other six.

The cause is worth naming. The reopen list was written from upstream's
`App.xaml.cs`, which enumerates the windows upstream restores, rather than from
what this client actually attaches. Copying the shape of the original without
checking the local half is how the entry came to exist for a window that could
never satisfy it.

`SettingsWindow` attaches now, on its parameterless constructor only. The other
constructor is handed throwaway state by tests, and reaching for `AppServices`
there would open the real settings file.

Two tests hold the invariant. The first reads the `Reopen` calls out of
`App.axaml.cs` and asserts every named window's source contains an `Attach`. The
second asserts no two windows attach to the same `WindowState`, since sharing one
would have them overwrite each other's position and fight over the closed flag.

Both read sources rather than exercising windows, which is unusual and worth
justifying. Constructing `MapWindow`, `DpsWindow`, `ConsoleWindow`,
`MobInfoWindow` or `SettingsWindow` the way the client does calls
`AppServices.Initialize`, which opens the live settings file, so a behavioural
version of this test would read a real configuration. The invariant is structural
in any case: the call is either written down or it is not.

### The guard for that had a decorative half

The uniqueness test written alongside it asserted nothing. Making `MapWindow`
attach to `DpsWindowState`, which is exactly the fault it exists to catch, left
the suite green.

The pattern was `Attach\(this,\s*[\w\.]*?(\w+WindowState)`. Five of the six
windows reach their state through `AppServices.Initialize().Bootstrap.Settings`,
and a character class of word characters and dots cannot cross the parentheses in
`Initialize()`. So the pattern matched one view, the overlay, and a single-item
list is unique against nothing. The count assertion underneath it, that at least
one match existed, was satisfied by that same lone entry.

It now matches up to the semicolon, and the count is compared against the number
of views that contain an `Attach` at all, so a pattern that stops matching fails
rather than quietly narrowing. With that, the mutation fails the test.

Fourth test this session that passed while proving nothing, and the fourth found
by breaking the code rather than by reading the test. Worth noting what they have
in common: each asserted over a collection that a bug made empty or single, where
the assertion is true and vacuous at the same time.

### Auditing this session's own additions

Two faults in this file were mine rather than upstream's: `Restore` written and
never called, and a reopen entry for a window that never attached. Both have the
same shape, something written that nothing reaches, so every member added during
this work was checked for a caller.

Twenty-one members across six files. Eighteen are reached from production code.
Three are not: `RegexSafety.Configured`, `TypeScale.DefaultTokens` and
`WindowPreferences.TryGetRequestedClickThrough`. All three exist so a test can
see that something took effect, which is a real category rather than dead code,
though it is public surface that only tests use and worth knowing about.

The first pass was wrong and flagged eight. It excluded each member's own file,
which hides every helper a class calls itself: `SendAsync` calls `IsAllowed`,
`Apply` calls `Compute`, `Compute` calls `ScaleToken`, `ScaleToCurrent` calls
`CurrentFactor`, `ApplyPreferencesTo` calls `TryGet`. Six false positives from
one bad exclusion, which is the same failure as every other bad measurement here:
the filter was written to match the expected answer rather than the question.

No new dead code. The two already found and fixed were the only instances.

### Why WindowState.State stays unread

Recorded as a judgement that was being avoided, which was too vague. There is a
concrete reason.

Upstream restores it verbatim, minimised included. Reopening now brings back
whatever was open at exit, so restoring the state as well would mean a window
minimised when the client closed reopens minimised: it is counted as open, it is
opened, and nothing appears. Leaving the field unread means such a window comes
back normal and visible, which is the better of the two.

Measured while checking this, on the headless backend:

```
normal position   400, 300
after minimise    400, 300   state=Minimized
captured rect     400,300 640x450
after close       Closed=True   ShouldReopen=False
```

A minimised window still reports usable coordinates, so `Capture` stores a real
rect rather than something meaningless, and the close path behaves. That is the
headless backend rather than a real display, so it says the logic holds, not that
macOS reports the same numbers.

One trap for anyone implementing this later. `WindowState` is ambiguous in this
codebase: `Avalonia.Controls.WindowState` and `EQTool.Models.WindowState` are
different types sharing a name, and restoring the field means assigning between
two same-named enums. `WindowPreferences` already qualifies its parameter for
that reason.

### Reset Triggers was mislabelled as a WPF port

I carried "night vision fix and Reset Triggers" as one optional item for several
rounds, on the grounds that both live in the uncompiled WPF
`SettingsGeneral.xaml.cs`. That was wrong for the second one. The reset itself is
three lines, and upstream's own test spells them out:

```csharp
settings.Triggers = new List<Trigger>();
settings.TriggerFolders = new List<TriggerFolder>();
_ = EQToolSettingsLoad.SyncBuiltInTriggers(settings);
```

`SyncBuiltInTriggers` is public static on `EQToolSettingsLoad`, which is compiled
into the Mac build, and `ResettingTriggersRestoresBuiltInDefaults` is one of the
41 linked upstream test files, so the semantics were already pinned and passing
before I wrote anything. Only the button was missing.

Night vision is a different matter and stays on the decision list: it writes
gamma settings into the user's EverQuest config, which is the same question as
writing `Log=TRUE` into `eqclient.ini`.

The one non-obvious bit of the implementation is `selected = null`. It looks
removable, because `Rebuild()` repopulates the visible list anyway, but the
selection holds a `Trigger` that reset has just discarded, so dropping it leaves
the detail pane bound to an object no longer in settings.

### Mistyped binding paths do not survive the build

I justified the render test on the assumption that a typo in a binding path fails
silently at runtime. That is the WPF behaviour, and it is the bug class this port
has produced twice already, so it sounded right. It is false here.

Mutating `TriggerEditor.ResetArmed` to `ResetArmd`, in the `CheckBox` `IsChecked`
and again in the `Button` `IsEnabled`, broke the build both times rather than
failing a test. Compiled bindings resolve paths at compile time in this project.

The render test still pays for itself, but for structural changes rather than
typos. Removing the `IsEnabled` attribute and renaming the button both compile
cleanly and both were caught. The commit message on `249a9f7b` overstates its
purpose and should be read against this entry.

### "Blocked on hardware" was two claims wearing one label

I carried `IsEqGameFocused` as blocked for several rounds because no `eqgame`
process exists on this machine to match against. That covered two separate
things: whether the detection mechanism works, and whether the name string is
reachable under Wine. Only the second needed EverQuest, and it turned out not to
need it either.

Wine is installed and the prefix holds 282 Windows executables, so
`notepad.exe` from `drive_c/windows` stands in for the game. Running it and
asking every available API about the same pid:

```
unix ps -o comm=    notepad.exe
proc_name           notepad.exe
proc_pidpath        /Applications/Wine Stable.app/.../wine
.NET ProcessName    wine
NSWorkspace         name=wine, bundle=(none), policy=0
```

Upstream compares `Process.ProcessName` to `eqgame`. That cannot work here:
.NET resolves the name through `proc_pidpath`, which is the Wine binary, so
every Wine-hosted program reports as `wine`. NSWorkspace is no better and does
not expose the Windows executable at all.

`proc_name` is the one call that returns it, and the pid NSWorkspace reports as
frontmost is the same pid `proc_name` resolves. So the frontmost pid comes from
AppKit and the name match stays in Core, which also keeps the AppKit dependency
out of a project that has none.

The prediction I had recorded was right for once. Matching on the NSWorkspace
name would have matched `wine` and treated any Wine program as EverQuest, which
is why `IsEqGame_ForTheWineWrapper_IsFalse` exists.

Two other things worth keeping. `AfkAttackedHandler` calls this before its
cooldown check, so it can fire several times a second during a fight; that ruled
out shelling out to `ps` and is why the implementation is a single P/Invoke.
And assigning a macOS-only method to a plain `Func<int?>` raises CA1416, because
the delegate could be invoked anywhere, so the platform guard lives inside the
lambda rather than around the assignment.

### A mutation that cannot be caught, and why it stays

`FrontmostProcessId?.Invoke()` survives being changed to `FrontmostProcessId
.Invoke()`. With no probe installed the null-conditional returns early, and
without it the `NullReferenceException` lands in the fail-safe catch. Both
produce `false`, so no test can tell them apart through the public surface.

`IsFocused_WithNoProbeInstalled_IsFalse` therefore asserts a true fact without
pinning the mechanism. The null-conditional stays because throwing for an
expected condition is the wrong shape, not because a test defends it. The catch
is deliberately broad: this runs inside log parsing and must never take the
parser down.

The sibling mutation, dropping `frontmost.Value <= 0`, did get pinned once
`IsFocused_WhenTheFrontmostPidIsNotReal_IsFalse` fed it a resolver that would
have answered `eqgame.exe` for pid 0.
