using System.Collections.Generic;
using System.Diagnostics;

namespace EQTool.Core.Platform
{
    public interface IProcessLauncher
    {
        void Start(string fileName, IReadOnlyList<string> arguments);
    }

    // Launches and forgets.
    //
    // Alerts fire from the log parse thread during combat, so nothing here may
    // block. Process objects hold an OS handle until disposed, and a raid can
    // fire hundreds of alerts, so each one disposes itself on exit rather than
    // accumulating handles for the lifetime of the session.
    public class ProcessLauncher : IProcessLauncher
    {
        public void Start(string fileName, IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Exited += (sender, _) => ((Process)sender).Dispose();

            _ = process.Start();
        }
    }
}
