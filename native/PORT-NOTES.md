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

## Follow-ups for Milestone 2

Milestone 1 proves the parsers run on `net9.0` on macOS. Milestone 2 should
be UI-shaped, not parser-shaped. Concrete next steps in priority order:

1. Native `SpellIcons` replacement. The stub returns an empty list, which
   means `EQSpells.BuildSpellInfo` skips every spell (`HasSpellIcon` is
   `false`). Either read the `.tga` resources with a cross-platform decoder
   (e.g. Pfim + `SkiaSharp`) or ship pre-baked PNGs and drop the icon step
   at load time.
2. Real `IAppDispatcher` bound to whatever UI toolkit Milestone 2 picks
   (Avalonia is already spiked under `mac/spike/`).
3. Wire a log-tail loop that pushes lines through the linked `LogParser` at
   `EQTool/Services/LogParser.cs`. That file is not linked yet — it pulls in
   handlers and settings I have not audited. Do that audit as the first step
   of Milestone 2.
4. Reuse upstream `EQtoolsTests/` where possible. The tests use Autofac and
   `Microsoft.VisualStudio.TestTools.UnitTesting` (MSTest), both of which
   work on .NET 9. What blocks reuse today is that `BaseTestClass` resolves a
   full DI graph including `SpellWindowViewModel` etc., which drags in
   WPF-heavy types we deliberately do not link. A future test project can
   link only the parser-focused test files (e.g. `DamageParserTests.cs`,
   `ZoneParsingTests.cs`, `FactionParserTests.cs`, `ParsingTests.cs`) once a
   headless DI container is available.
