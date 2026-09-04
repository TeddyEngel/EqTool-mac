using System;
using System.IO;

namespace EQTool.Core.Platform
{
    // Keeps settings.json outside the build output.
    //
    // EQToolSettingsLoad computes its path once, at type initialisation, from
    // Paths.InExecutableDirectory("settings.json"). The field is private static
    // readonly and both Load and Save are non-virtual, so there is no seam to
    // override and LogParser takes the concrete type regardless. On a normal
    // dotnet layout that puts user settings inside bin/, where `dotnet clean`
    // deletes them.
    //
    // Rather than edit upstream, leave the path alone and change what lives at it:
    // put the real file under ~/Library/Application Support/PigParse and drop a
    // symlink in the build output. File.WriteAllText opens with O_CREAT|O_TRUNC,
    // which follows symlinks, so upstream writes land on the real file. No copying,
    // so no window where the 100ms save timer races a sync step.
    public static class MacSettingsStore
    {
        public const string SettingsFileName = "settings.json";

        private const string ApplicationDirectoryName = "PigParse";

        public static string DefaultCanonicalDirectory()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", ApplicationDirectoryName);
        }

        // Returns the canonical file the executable-directory path now resolves to.
        public static string EnsureRedirected(string executableDirectory, string canonicalDirectory)
        {
            if (string.IsNullOrWhiteSpace(executableDirectory))
                throw new ArgumentException("Executable directory is required.", nameof(executableDirectory));

            if (string.IsNullOrWhiteSpace(canonicalDirectory))
                throw new ArgumentException("Canonical directory is required.", nameof(canonicalDirectory));

            _ = Directory.CreateDirectory(canonicalDirectory);

            var linkPath = Path.Combine(executableDirectory, SettingsFileName);
            var canonicalPath = Path.Combine(canonicalDirectory, SettingsFileName);

            if (AlreadyPointsAtCanonical(linkPath, canonicalPath))
                return canonicalPath;

            MigrateExistingSettings(linkPath, canonicalPath);

            if (File.Exists(linkPath) || IsSymbolicLink(linkPath))
                File.Delete(linkPath);

            _ = File.CreateSymbolicLink(linkPath, canonicalPath);

            return canonicalPath;
        }

        private static bool AlreadyPointsAtCanonical(string linkPath, string canonicalPath)
        {
            var target = ResolveLinkTargetOrNull(linkPath);
            if (target == null)
                return false;

            return string.Equals(target, canonicalPath, StringComparison.Ordinal);
        }

        // A real settings.json already in the build output is a user's existing
        // configuration from before this redirect existed. Move it rather than
        // deleting it, but never overwrite a canonical file that already exists.
        private static void MigrateExistingSettings(string linkPath, string canonicalPath)
        {
            if (IsSymbolicLink(linkPath))
                return;

            if (!File.Exists(linkPath))
                return;

            if (File.Exists(canonicalPath))
                return;

            File.Move(linkPath, canonicalPath);
        }

        private static bool IsSymbolicLink(string path)
        {
            return ResolveLinkTargetOrNull(path) != null;
        }

        private static string ResolveLinkTargetOrNull(string path)
        {
            try
            {
                return File.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
