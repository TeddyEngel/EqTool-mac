using EQTool.Models;
using EQTool.Services;
using System.Collections.Generic;

namespace EQTool.Core.Platform
{
    // Spoken trigger alerts on macOS.
    //
    // Upstream's TextToSpeach is System.Speech.Synthesis, which does not exist
    // off Windows, and the Linux build compiles it out entirely so those triggers
    // are silent there. /usr/bin/say ships with macOS, queues utterances by
    // itself, and needs no permission, so it covers the alert case without
    // interop or a dependency.
    public class MacTextToSpeach : ITextToSpeach
    {
        private const string SayExecutable = "/usr/bin/say";

        private readonly IProcessLauncher processLauncher;
        private readonly EQToolSettings settings;

        public MacTextToSpeach(IProcessLauncher processLauncher, EQToolSettings settings)
        {
            this.processLauncher = processLauncher;
            this.settings = settings;
        }

        public void Say(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var arguments = new List<string>();

            var voice = settings?.SelectedVoice;
            if (!string.IsNullOrWhiteSpace(voice))
            {
                arguments.Add("-v");
                arguments.Add(voice);
            }

            // ArgumentList escapes each entry, so the phrase is passed as one
            // argument and never reaches a shell for interpretation.
            arguments.Add(text);

            processLauncher.Start(SayExecutable, arguments);
        }
    }
}
