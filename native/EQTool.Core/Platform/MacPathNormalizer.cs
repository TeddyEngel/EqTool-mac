using System;

namespace EQTool.Core.Platform
{
    // Converts the Windows and Wine paths that PigParse stores in settings.json into
    // native macOS paths.
    //
    // This exists because upstream's Paths.Combine and UIFileName.TryParse are correct
    // for native input but corrupt Windows input silently rather than rejecting it.
    // Paths.Combine("C:\EQ\", "eqclient.ini") yields "C:\EQ\/eqclient.ini" on macOS,
    // because '\' is not a separator here so the trim never fires. Worse,
    // UIFileName.TryParse("C:\EQ\UI_Pigy_P1999Green.ini") returns true with a PlayerName
    // of "C:\EQ\UI_Pigy". Neither throws.
    //
    // Anyone moving from the Wine build to the native client carries a settings.json
    // full of Windows paths, so normalise at that boundary rather than changing
    // upstream, which would break the Windows build.
    public static class MacPathNormalizer
    {
        private const string DriveDirectoryPrefix = "drive_";

        // Wine maps Z: to the host filesystem root.
        private const char HostRootDriveLetter = 'Z';

        public static bool TryNormalize(string path, string winePrefix, out string normalizedPath)
        {
            normalizedPath = null;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            var trimmedPath = path.Trim();

            if (trimmedPath.StartsWith("/", StringComparison.Ordinal))
            {
                normalizedPath = trimmedPath.Replace('\\', '/');
                return true;
            }

            if (!HasDriveLetter(trimmedPath))
            {
                normalizedPath = trimmedPath.Replace('\\', '/');
                return true;
            }

            var driveLetter = char.ToUpperInvariant(trimmedPath[0]);
            var remainder = trimmedPath.Substring(2).Replace('\\', '/').TrimStart('/');

            if (driveLetter == HostRootDriveLetter)
            {
                normalizedPath = "/" + remainder;
                return true;
            }

            if (string.IsNullOrWhiteSpace(winePrefix))
                return false;

            var prefixRoot = winePrefix.TrimEnd('/');
            var driveDirectory = DriveDirectoryPrefix + char.ToLowerInvariant(driveLetter);

            normalizedPath = remainder.Length == 0
                ? prefixRoot + "/" + driveDirectory
                : prefixRoot + "/" + driveDirectory + "/" + remainder;

            return true;
        }

        private static bool HasDriveLetter(string path)
        {
            if (path.Length < 2)
                return false;

            if (path[1] != ':')
                return false;

            return char.IsLetter(path[0]);
        }
    }
}
