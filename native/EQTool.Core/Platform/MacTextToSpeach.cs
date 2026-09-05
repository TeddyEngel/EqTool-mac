using EQTool.Models;
using EQTool.Services;
using System.Collections.Generic;
using System.Globalization;

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
        private const int FullVolumePercent = 100;

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

            // Upstream sets the synthesizer's Volume from this. `say` has no
            // volume flag, so the only route is an inline speech command, which
            // means it has to ride on the phrase itself.
            var volumePercent = System.Math.Clamp(settings?.GlobalAudioVolume ?? FullVolumePercent, 0, FullVolumePercent);
            if (volumePercent <= 0)
                return;

            var phrase = text;
            if (volumePercent < FullVolumePercent)
            {
                // Only when it is actually turned down: `[[volm 1.0]]` is what
                // `say` already does, so adding it at full volume would change
                // every phrase for no effect.
                var volume = volumePercent / (double)FullVolumePercent;

                // Invariant, or a comma decimal separator turns the command into
                // something `say` speaks out loud instead of obeying.
                phrase = "[[volm " + volume.ToString("0.###", CultureInfo.InvariantCulture) + "]]" + text;
            }

            // ArgumentList escapes each entry, so the phrase is passed as one
            // argument and never reaches a shell for interpretation.
            arguments.Add(phrase);

            processLauncher.Start(SayExecutable, arguments);
        }
    }
}
