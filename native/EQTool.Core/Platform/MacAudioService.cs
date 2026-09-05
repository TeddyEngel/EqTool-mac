using EQTool.Models;
using EQTool.Services;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace EQTool.Core.Platform
{
    // Sound-file trigger alerts on macOS.
    //
    // The linked AudioService is built on System.Windows.Media.MediaPlayer, which
    // resolves to an empty shim here, so it silently plays nothing while looking
    // wired in the container. This replaces it.
    //
    // /usr/bin/afplay is one process per sound, which is exactly what overlapping
    // alerts need: a raid can fire several at once and each plays to completion
    // rather than cutting off the previous one, which is what upstream's single
    // MediaPlayer instance does on Windows.
    public class MacAudioService : IAudioService
    {
        private const string AfplayExecutable = "/usr/bin/afplay";
        private const int DefaultVolumePercent = 100;

        private readonly IProcessLauncher processLauncher;
        private readonly EQToolSettings settings;

        public MacAudioService(IProcessLauncher processLauncher, EQToolSettings settings)
        {
            this.processLauncher = processLauncher;
            this.settings = settings;
        }

        public void Play(string soundFilePath)
        {
            if (string.IsNullOrWhiteSpace(soundFilePath) || !File.Exists(soundFilePath))
                return;

            var volumePercent = settings?.GlobalAudioVolume ?? DefaultVolumePercent;
            var volume = System.Math.Clamp(volumePercent, 0, 100) / 100.0;

            if (volume <= 0)
                return;

            var arguments = new List<string>
            {
                "-v",
                volume.ToString("0.###", CultureInfo.InvariantCulture),
                soundFilePath
            };

            processLauncher.Start(AfplayExecutable, arguments);
        }
    }
}
