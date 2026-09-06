using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EQTool.Core.Platform
{
    public enum EqLoggingEnableOutcome
    {
        Enabled,
        AlreadyEnabled,
        NoConfigFile,
        Failed
    }

    public sealed class EqLoggingEnableResult
    {
        public EqLoggingEnableOutcome Outcome { get; set; }

        public string Message { get; set; }

        public string BackupPath { get; set; }
    }

    public static class EqLoggingEnabler
    {
        public const string ConfigFileName = "eqclient.ini";
        public const string BackupSuffix = ".pigparse-backup";

        public static EqLoggingEnableResult Enable(string eqDirectory)
        {
            if (string.IsNullOrWhiteSpace(eqDirectory))
                return Fail("No EverQuest directory is configured.");

            var configPath = Path.Combine(eqDirectory, ConfigFileName);
            if (!File.Exists(configPath))
                return new EqLoggingEnableResult
                {
                    Outcome = EqLoggingEnableOutcome.NoConfigFile,
                    Message = $"No {ConfigFileName} in {eqDirectory}."
                };

            try
            {
                var lines = File.ReadAllLines(configPath);
                if (lines.Any(IsLogTrue))
                    return new EqLoggingEnableResult
                    {
                        Outcome = EqLoggingEnableOutcome.AlreadyEnabled,
                        Message = "Logging was already on."
                    };

                var backupPath = configPath + BackupSuffix;
                File.Copy(configPath, backupPath, true);

                // Upstream only rewrites an existing Log= line, so a file without
                // one is left unchanged and logging stays off. Appending covers
                // that case.
                var rewritten = new List<string>();
                var replaced = false;
                foreach (var line in lines)
                {
                    if (IsLogSetting(line))
                    {
                        rewritten.Add("Log=TRUE");
                        replaced = true;
                    }
                    else
                    {
                        rewritten.Add(line);
                    }
                }

                if (!replaced)
                    rewritten.Add("Log=TRUE");

                File.WriteAllLines(configPath, rewritten);

                return new EqLoggingEnableResult
                {
                    Outcome = EqLoggingEnableOutcome.Enabled,
                    Message = replaced
                        ? "Logging turned on. Restart EverQuest for it to take effect."
                        : "Logging turned on by adding the missing line. Restart EverQuest for it to take effect.",
                    BackupPath = backupPath
                };
            }
            catch (Exception failure)
            {
                return Fail(failure.Message);
            }
        }

        private static bool IsLogSetting(string line)
        {
            return Normalize(line).StartsWith("log=", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLogTrue(string line)
        {
            return string.Equals(Normalize(line), "log=true", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string line)
        {
            return (line ?? string.Empty).Trim().Replace(" ", string.Empty);
        }

        private static EqLoggingEnableResult Fail(string message)
        {
            return new EqLoggingEnableResult
            {
                Outcome = EqLoggingEnableOutcome.Failed,
                Message = message
            };
        }
    }
}
