using System;
using System.Runtime.InteropServices;
using System.Text;

namespace EQTool.Core.Platform
{
    public static class EqGameFocus
    {
        private const string EqGameProcessName = "eqgame";

        // proc_pidpath, which is what Process.ProcessName uses on macOS, reports the Wine
        // binary for a Wine-hosted program. proc_name reports the Windows executable.
        [DllImport("libproc", SetLastError = true)]
        private static extern int proc_name(int pid, byte[] buffer, uint buffersize);

        public static Func<int?> FrontmostProcessId { get; set; }

        public static Func<int, string> ResolveProcessName { get; set; } = NativeProcessName;

        public static string NativeProcessName(int processId)
        {
            var buffer = new byte[256];
            var written = proc_name(processId, buffer, (uint)buffer.Length);
            return written <= 0 ? null : Encoding.UTF8.GetString(buffer, 0, written);
        }

        public static string NormalizeProcessName(string rawProcessName)
        {
            if (string.IsNullOrWhiteSpace(rawProcessName))
                return string.Empty;

            var trimmed = rawProcessName.Trim();

            // Wine reports Windows paths for some processes and unix paths for others.
            var lastSeparator = trimmed.LastIndexOfAny(new[] { '\\', '/' });
            if (lastSeparator >= 0)
                trimmed = trimmed.Substring(lastSeparator + 1);

            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - 4);

            return trimmed;
        }

        public static bool IsEqGame(string rawProcessName)
        {
            return string.Equals(
                NormalizeProcessName(rawProcessName),
                EqGameProcessName,
                StringComparison.OrdinalIgnoreCase);
        }

        // Returning false means "the player is not looking at the game", which lets the
        // AFK alert through. Every failure path keeps that side so a broken probe warns
        // too often rather than staying silent during an attack.
        public static bool IsFocused()
        {
            try
            {
                var frontmost = FrontmostProcessId?.Invoke();
                if (frontmost == null || frontmost.Value <= 0)
                    return false;

                return IsEqGame(ResolveProcessName?.Invoke(frontmost.Value));
            }
            catch
            {
                return false;
            }
        }
    }
}
