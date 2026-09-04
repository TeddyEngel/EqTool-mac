using System;
using System.IO;

namespace EQTool.Avalonia.Services
{
    // `MacSettingsPathResolver` needs a Wine prefix to turn a `C:\...` path from
    // a carried-over settings.json into a real macOS path. Nothing else needs
    // one: a settings file that already holds POSIX paths resolves without it.
    //
    // Search order, first hit wins:
    //   1. PIGPARSE_WINEPREFIX  - explicit override for this app
    //   2. WINEPREFIX           - whatever the shell is already pointed at
    //   3. ~/.wine-pigparse     - the prefix the macOS spike created
    //   4. ~/.wine              - the Wine default
    public static class WinePrefixLocator
    {
        public static string Locate()
        {
            var explicitPrefix = Environment.GetEnvironmentVariable("PIGPARSE_WINEPREFIX");
            if (!string.IsNullOrWhiteSpace(explicitPrefix))
                return explicitPrefix;

            var shellPrefix = Environment.GetEnvironmentVariable("WINEPREFIX");
            if (!string.IsNullOrWhiteSpace(shellPrefix))
                return shellPrefix;

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                return null;

            var spikePrefix = Path.Combine(home, ".wine-pigparse");
            if (Directory.Exists(spikePrefix))
                return spikePrefix;

            var defaultPrefix = Path.Combine(home, ".wine");
            if (Directory.Exists(defaultPrefix))
                return defaultPrefix;

            return null;
        }
    }
}
