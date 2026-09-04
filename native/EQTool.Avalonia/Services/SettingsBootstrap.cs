using System.IO;
using EQTool.Core.Platform;
using EQTool.Models;
using EQTool.Services;

namespace EQTool.Avalonia.Services
{
    public class SettingsBootstrapResult
    {
        public EQToolSettings Settings { get; set; }

        public EQToolSettingsLoad Loader { get; set; }

        // False when a path in settings.json could not be turned into a macOS
        // path (a `C:\...` path with no Wine prefix to resolve it against). The
        // original value is left untouched in that case, so the UI can say so
        // rather than tailing a directory that does not exist.
        public bool EqDirectoryResolved { get; set; }

        public bool LogDirectoryResolved { get; set; }

        public bool LogDirectoryUsable
            => LogDirectoryResolved
               && !string.IsNullOrWhiteSpace(Settings?.EqLogDirectory)
               && Directory.Exists(Settings.EqLogDirectory);
    }

    // Loads settings the way the app does, then normalises the two path-bearing
    // fields before anything reads them. `LogParser.Poll` feeds both straight
    // into `FindEq.GetLogFileLocation` on a 100 ms timer, so the rewrite has to
    // happen before the parser is ever constructed.
    public static class SettingsBootstrap
    {
        public static SettingsBootstrapResult Load()
        {
            var loader = new EQToolSettingsLoad(new FindEq(), new LoggingService());
            var settings = loader.Load();
            var resolution = MacSettingsPathResolver.Resolve(settings, WinePrefixLocator.Locate());

            return new SettingsBootstrapResult
            {
                Settings = settings,
                Loader = loader,
                EqDirectoryResolved = resolution.DefaultEqDirectoryResolved,
                LogDirectoryResolved = resolution.EqLogDirectoryResolved
            };
        }
    }
}
