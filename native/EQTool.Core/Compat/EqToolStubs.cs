// Stubs for a small number of upstream service classes whose *implementations*
// pull in Windows-only APIs (System.Drawing.Bitmap, TGASharpLib, WPF's App
// singleton, HttpClient POSTs at construction time). The parsers never call
// the members backing those APIs at test time; they only need the *types* to
// satisfy constructor parameters and field declarations.
//
// Declaring these types in their upstream namespaces lets us drop the real
// files from the compile without editing them. If Milestone 2 later needs the
// real behaviour (e.g. spell icons in the native UI), it can either link the
// real files with proper native replacements or reimplement them.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EQTool.Models;
using EQToolShared.Enums;

// Stub for the WPF App singleton. Upstream references App.Version and
// App.Current.Dispatcher from a couple of non-parser files that we still link
// in. Nothing in the parsing path actually reads these, but the compiler needs
// the symbols.
namespace EQTool
{
    public static class App
    {
        public static string Version => "0.0-mac-core";
        public static string VersionType => "Mac";
    }
}

namespace EQTool.Services
{
    // Real upstream IAppDispatcher (EQTool/Services/AppDispatcher.cs) targets
    // WPF's Dispatcher via App.Current.Dispatcher. The parsers only depend on
    // the interface for constructor injection; a plain synchronous
    // implementation is fine for a headless core.
    public interface IAppDispatcher
    {
        void DispatchUI(Action action);
        void DebounceToUI(ref CancellationTokenSource debounceCancellationSource, int delay, Action action);
        void DebounceToUI(ref CancellationTokenSource debounceCancellationSource, int delay, Action action, Func<bool> shouldCancel);
    }

    public class AppDispatcher : IAppDispatcher
    {
        public void DispatchUI(Action action)
        {
            action?.Invoke();
        }

        public void DebounceToUI(ref CancellationTokenSource debounceCancellationSource, int delay, Action action)
        {
            DebounceToUI(ref debounceCancellationSource, delay, action, () => false);
        }

        public void DebounceToUI(ref CancellationTokenSource debounceCancellationSource, int delay, Action action, Func<bool> shouldCancel)
        {
            action?.Invoke();
        }
    }

    // BinaryFormatter is disabled in .NET 9. The parsers never persist anything
    // through this helper; only settings-load code paths do, and we do not link
    // those. Provide the surface so ParseSpells_spells_us compiles.
    public static class BinarySerializer
    {
        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite)
        {
            throw new NotSupportedException("BinarySerializer is not available in the native core.");
        }

        public static T ReadFromBinaryFile<T>(string filePath)
        {
            throw new NotSupportedException("BinarySerializer is not available in the native core.");
        }
    }

    // Real upstream LoggingService (EQTool/Services/LoggingService.cs) references
    // WPF's App class for version metadata and posts crash reports over HTTP on
    // every call. Neither is desirable in a headless parsing core.
    public class LoggingService
    {
        public void Log(string message, EventType eventType, Servers? server)
        {
            // Intentionally empty: parsing does not need remote error reporting.
        }
    }

    // Real upstream SpellIcons (EQTool/Services/Spells/SpellIcons.cs) decodes
    // embedded .tga icon resources using TGASharpLib and System.Drawing.Bitmap.
    // Parsers only need EQSpells to be constructible and to walk the spell list;
    // Spell.HasSpellIcon is computed from mapped indices, so returning an empty
    // icon list here means Spell.Map(...) will report HasSpellIcon = false and
    // EQSpells.BuildSpellInfo will skip every entry.
    //
    // Tests that need populated spell lookups can hand EQSpells the real
    // ParseSpells_spells_us output and inject their own SpellIcon list via a
    // custom subclass of this stub, or exercise the parsers in isolation
    // without going through EQSpells.
    public class SpellIcons
    {
        public SpellIcons(EQToolSettings settings)
        {
            // settings intentionally ignored in the stub.
        }

        public virtual List<SpellIcon> GetSpellIcons(Servers servers)
        {
            return new List<SpellIcon>();
        }
    }
}
