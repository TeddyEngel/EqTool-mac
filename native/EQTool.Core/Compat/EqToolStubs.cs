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
using System.Net.Http;
using System.Threading;
using EQTool.Models;
using EQToolShared.Enums;

namespace EQTool
{
    public static class App
    {
        // Upstream sets the regex match timeout in this class's static ctor, and
        // things that reach App before Main should get the same guarantee.
        static App()
        {
            EQTool.Core.Platform.RegexSafety.Install();
        }

        public static string Version => "0.0-mac-core";
        public static string VersionType => "Mac";

        // Must not become a bare HttpClient: PlayerTrackerService is reachable
        // and posts character data unprompted. See PigParseNetworkGuard.
        public static readonly HttpClient httpclient =
            new HttpClient(new EQTool.Core.Platform.PigParseNetworkGuard());

        public static void LogUnhandledException(Exception exception, string source, Servers? server)
        {
        }
    }
}

namespace EQTool.Services
{
    public interface ITextToSpeach
    {
        void Say(string text);
    }

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

    public class LoggingService
    {
        public void Log(string message, EventType eventType, Servers? server)
        {
        }
    }

    public class SpellIcons
    {
        public SpellIcons(EQToolSettings settings)
        {
        }

        public virtual List<SpellIcon> GetSpellIcons(Servers servers)
        {
            var list = new List<SpellIcon>();
            for (var spellFileIndex = 1; spellFileIndex <= 7; spellFileIndex++)
            {
                list.Add(new SpellIcon { SpellIndex = spellFileIndex });
            }
            return list;
        }
    }

    public static class ForegroundWindowHelper
    {
        public static bool IsEqGameFocused()
        {
            return false;
        }
    }
}

namespace EQTool.ViewModels.SettingsComponents
{
    public enum MobInfoItemType
    {
        Mob,
        Pet,
    }
}

namespace EQTool.ViewModels.MobInfoComponents
{
    public class MobInfoManagementViewModel
    {
        public EQTool.ViewModels.SettingsComponents.MobInfoItemType MobInfoItemType { get; set; }
    }
}

namespace EQTool.ViewModels
{
    public class SettingsWindowViewModel
    {
        public string GroupLeaderName { get; set; } = "None";
    }
}

