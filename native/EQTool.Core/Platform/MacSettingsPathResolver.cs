using EQTool.Models;

namespace EQTool.Core.Platform
{
    public class MacSettingsPathResolution
    {
        public bool DefaultEqDirectoryResolved { get; set; }

        public bool EqLogDirectoryResolved { get; set; }

        public bool AllResolved => DefaultEqDirectoryResolved && EqLogDirectoryResolved;
    }

    // Rewrites the two path-bearing settings into native macOS form before anything
    // reads them.
    //
    // LogParser.Poll feeds settings.DefaultEqDirectory and settings.EqLogDirectory
    // straight into FindEq.GetLogFileLocation on a 100ms timer. A settings.json
    // carried over from the Wine build holds Windows paths, and the downstream
    // helpers corrupt those silently rather than rejecting them, so they have to be
    // converted before the timer ever runs.
    //
    // A path that cannot be resolved is left exactly as it was rather than being
    // replaced with a guess. The caller gets a false flag and should prompt the user
    // to pick the directory again.
    public static class MacSettingsPathResolver
    {
        public static MacSettingsPathResolution Resolve(EQToolSettings settings, string winePrefix)
        {
            var resolution = new MacSettingsPathResolution();

            if (settings == null)
                return resolution;

            resolution.DefaultEqDirectoryResolved = TryRewrite(
                settings.DefaultEqDirectory,
                winePrefix,
                out var resolvedDefaultEqDirectory);

            if (resolution.DefaultEqDirectoryResolved)
                settings.DefaultEqDirectory = resolvedDefaultEqDirectory;

            resolution.EqLogDirectoryResolved = TryRewrite(
                settings.EqLogDirectory,
                winePrefix,
                out var resolvedEqLogDirectory);

            if (resolution.EqLogDirectoryResolved)
                settings.EqLogDirectory = resolvedEqLogDirectory;

            return resolution;
        }

        // An unset path is not a failure. There is nothing to convert, and the app
        // already handles a missing directory by asking the user for one.
        private static bool TryRewrite(string value, string winePrefix, out string rewritten)
        {
            rewritten = value;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            return MacPathNormalizer.TryNormalize(value, winePrefix, out rewritten);
        }
    }
}
