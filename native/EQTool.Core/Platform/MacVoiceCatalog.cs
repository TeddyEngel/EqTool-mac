using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EQTool.Core.Platform
{
    public class MacVoice
    {
        public MacVoice(string name, string locale)
        {
            Name = name;
            Locale = locale;
        }

        public string Name { get; }

        public string Locale { get; }

        public override string ToString() => Name + "  (" + Locale + ")";
    }

    // Lists the voices /usr/bin/say can use, so the voice setting offers real
    // choices rather than a free-text field that silently fails when misspelled.
    //
    // `say -v ?` prints fixed-width columns:
    //   Albert              en_US    # Hello! My name is Albert.
    // The name can contain spaces and non-ASCII (Amélie), so it is taken as
    // everything before the locale token rather than by splitting on whitespace.
    public static class MacVoiceCatalog
    {
        private const string SayExecutable = "/usr/bin/say";

        public static IReadOnlyList<MacVoice> Parse(string sayOutput)
        {
            var voices = new List<MacVoice>();

            if (string.IsNullOrWhiteSpace(sayOutput))
                return voices;

            var lines = sayOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var commentIndex = line.IndexOf('#');
                var beforeComment = commentIndex >= 0 ? line.Substring(0, commentIndex) : line;

                var parts = beforeComment.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                var locale = parts[parts.Length - 1];
                var name = string.Join(" ", parts.Take(parts.Length - 1)).Trim();

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                voices.Add(new MacVoice(name, locale));
            }

            return voices;
        }

        // English voices first: the log lines being spoken are English, and a
        // non-English voice reading them is unintelligible rather than merely
        // accented.
        public static IReadOnlyList<MacVoice> Available()
        {
            var output = RunSayListing();
            return Parse(output)
                .OrderByDescending(a => a.Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string RunSayListing()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = SayExecutable,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                startInfo.ArgumentList.Add("-v");
                startInfo.ArgumentList.Add("?");

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        return string.Empty;

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);
                    return output;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
