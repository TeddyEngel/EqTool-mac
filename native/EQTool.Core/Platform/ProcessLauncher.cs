using System;
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

            // Drained, not merely redirected. A child that fills the pipe buffer
            // blocks on write forever, so it never exits, Exited never fires and
            // the handle is never released. Measured: 200KB of output with both
            // pipes redirected and unread does not exit; unredirected it does.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.Exited += (sender, _) => ((Process)sender).Dispose();

            try
            {
                _ = process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception)
            {
                // This runs on the log parse thread during combat. A missing
                // executable must not stop the parser.
                process.Dispose();
            }
        }
    }
}
