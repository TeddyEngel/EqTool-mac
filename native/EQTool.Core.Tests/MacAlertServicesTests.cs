using EQTool.Core.Platform;
using EQTool.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace EQTool.Core.Tests
{
    public class RecordingProcessLauncher : IProcessLauncher
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Launches { get; }
            = new List<(string, IReadOnlyList<string>)>();

        public void Start(string fileName, IReadOnlyList<string> arguments)
        {
            Launches.Add((fileName, arguments));
        }
    }

    [TestClass]
    public class MacTextToSpeachTests
    {
        private RecordingProcessLauncher launcher;
        private EQToolSettings settings;
        private MacTextToSpeach speech;

        [TestInitialize]
        public void Setup()
        {
            launcher = new RecordingProcessLauncher();
            settings = new EQToolSettings();
            speech = new MacTextToSpeach(launcher, settings);
        }

        [TestMethod]
        public void Say_PlainText_InvokesSayWithThePhrase()
        {
            // Act
            speech.Say("Dragon Roar");

            // Assert
            Assert.AreEqual(1, launcher.Launches.Count);
            Assert.AreEqual("/usr/bin/say", launcher.Launches[0].FileName);
            CollectionAssert.Contains((System.Collections.ICollection)launcher.Launches[0].Arguments, "Dragon Roar");
        }

        [TestMethod]
        public void Say_WithSelectedVoice_PassesTheVoiceFlag()
        {
            // Arrange
            settings.SelectedVoice = "Samantha";

            // Act
            speech.Say("Enrage");

            // Assert
            var arguments = launcher.Launches[0].Arguments;
            Assert.AreEqual("-v", arguments[0]);
            Assert.AreEqual("Samantha", arguments[1]);
            Assert.AreEqual("Enrage", arguments[2]);
        }

        [TestMethod]
        public void Say_WithoutSelectedVoice_OmitsTheVoiceFlag()
        {
            // Act
            speech.Say("Enrage");

            // Assert
            var arguments = launcher.Launches[0].Arguments;
            Assert.AreEqual(1, arguments.Count);
            Assert.AreEqual("Enrage", arguments[0]);
        }

        [TestMethod]
        public void Say_TextWithQuotesAndSemicolons_IsPassedAsOneArgument()
        {
            // Arrange
            // ArgumentList escapes per entry, so shell metacharacters in a trigger's
            // spoken text are data rather than something a shell could act on.
            var hostile = "\"; rm -rf /tmp/x; echo \"";

            // Act
            speech.Say(hostile);

            // Assert
            var arguments = launcher.Launches[0].Arguments;
            Assert.AreEqual(1, arguments.Count);
            Assert.AreEqual(hostile, arguments[0]);
        }

        [TestMethod]
        public void Say_EmptyText_DoesNotLaunchAnything()
        {
            // Act
            speech.Say("   ");

            // Assert
            Assert.AreEqual(0, launcher.Launches.Count);
        }
    }

    [TestClass]
    public class MacAudioServiceTests
    {
        private RecordingProcessLauncher launcher;
        private EQToolSettings settings;
        private MacAudioService audio;
        private string existingFile;

        [TestInitialize]
        public void Setup()
        {
            launcher = new RecordingProcessLauncher();
            settings = new EQToolSettings();
            audio = new MacAudioService(launcher, settings);

            existingFile = Path.Combine(Path.GetTempPath(), "pigparse-alert-" + Path.GetRandomFileName() + ".wav");
            File.WriteAllBytes(existingFile, new byte[] { 0x52, 0x49, 0x46, 0x46 });
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(existingFile))
                File.Delete(existingFile);
        }

        [TestMethod]
        public void Play_ExistingFile_InvokesAfplayWithThePath()
        {
            // Act
            audio.Play(existingFile);

            // Assert
            Assert.AreEqual(1, launcher.Launches.Count);
            Assert.AreEqual("/usr/bin/afplay", launcher.Launches[0].FileName);
            CollectionAssert.Contains((System.Collections.ICollection)launcher.Launches[0].Arguments, existingFile);
        }

        [TestMethod]
        public void Play_HalfVolume_MapsPercentOntoAfplayScale()
        {
            // Arrange
            settings.GlobalAudioVolume = 50;

            // Act
            audio.Play(existingFile);

            // Assert
            var arguments = launcher.Launches[0].Arguments;
            Assert.AreEqual("-v", arguments[0]);
            Assert.AreEqual("0.5", arguments[1]);
        }

        [TestMethod]
        public void Play_ZeroVolume_DoesNotLaunchAnything()
        {
            // Arrange
            settings.GlobalAudioVolume = 0;

            // Act
            audio.Play(existingFile);

            // Assert
            Assert.AreEqual(0, launcher.Launches.Count);
        }

        [TestMethod]
        public void Play_MissingFile_DoesNotLaunchAnything()
        {
            // Act
            audio.Play(Path.Combine(Path.GetTempPath(), "definitely-not-here.wav"));

            // Assert
            Assert.AreEqual(0, launcher.Launches.Count);
        }

        [TestMethod]
        public void Play_CalledConcurrently_LaunchesOnePerSound()
        {
            // Arrange
            // Overlapping alerts are the point: upstream's single MediaPlayer stops
            // the previous sound, whereas one afplay per sound lets them coexist.

            // Act
            audio.Play(existingFile);
            audio.Play(existingFile);
            audio.Play(existingFile);

            // Assert
            Assert.AreEqual(3, launcher.Launches.Count);
        }
    }
}
