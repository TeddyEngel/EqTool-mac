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

### Not yet wired up

Nothing calls this yet — there is no native settings loader to call it from.
Milestone 2 must run every path read from `settings.json` through it before
handing anything to `Paths.Combine` or `UIFileName.TryParse`.
