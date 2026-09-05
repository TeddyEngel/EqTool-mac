using EQTool.Avalonia.ViewModels;
using EQTool.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace EQTool.Avalonia.Tests
{
    // The client reads EverQuest's log file and nothing else. With EverQuest's
    // own logging switched off the file never grows, so every window stays empty
    // and there was no sign of why: the settings window checked whether the log
    // folder was set and never whether anything was being written to it.
    //
    // FindEq.TryCheckLoggingEnabled was already compiled into this build and
    // simply was not called.
    [TestClass]
    public class EqLoggingDetectionTests
    {
        private string eqDirectory;

        [TestInitialize]
        public void Setup()
        {
            eqDirectory = Path.Combine(Path.GetTempPath(), "pigparse-eq-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(eqDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(eqDirectory))
                    Directory.Delete(eqDirectory, recursive: true);
            }
            catch
            {
            }
        }

        private void WriteIni(string logLine)
        {
            File.WriteAllLines(
                Path.Combine(eqDirectory, "eqclient.ini"),
                new[] { "[Defaults]", "Width=1920", logLine, "Gamma=1.0" });
        }

        private SettingsWindowViewModel CreateViewModel(string directory)
        {
            var settings = new EQToolSettings
            {
                Triggers = new List<Trigger>(),
                DefaultEqDirectory = directory,
            };
            var speech = new RecordingTextToSpeach();
            var triggerEditor = new TriggerEditorViewModel(
                settings, () => { }, speech, new RecordingAudioService());

            return new SettingsWindowViewModel(settings, () => { }, speech, triggerEditor);
        }

        [TestMethod]
        public void EqLoggingIsOff_WhenTheIniSaysFalse_Warns()
        {
            // Arrange
            WriteIni("Log=FALSE");

            // Act
            var viewModel = CreateViewModel(eqDirectory);

            // Assert
            Assert.IsTrue(viewModel.EqLoggingIsOff);
        }

        [TestMethod]
        public void EqLoggingIsOff_WhenTheIniSaysTrue_DoesNotWarn()
        {
            // Arrange
            WriteIni("Log=TRUE");

            // Act
            var viewModel = CreateViewModel(eqDirectory);

            // Assert
            Assert.IsFalse(viewModel.EqLoggingIsOff);
        }

        [TestMethod]
        public void EqLoggingIsOff_WhenTheIniIsMissing_DoesNotWarn()
        {
            // Arrange
            // TryCheckLoggingEnabled cannot tell and returns null. Treating that
            // as "off" would show the warning to everyone whose install has not
            // been located, which is most people on first run.

            // Act
            var viewModel = CreateViewModel(eqDirectory);

            // Assert
            Assert.IsFalse(viewModel.EqLoggingIsOff);
        }

        [TestMethod]
        public void EqLoggingIsOff_WithNoDirectorySet_DoesNotWarn()
        {
            // Act
            var viewModel = CreateViewModel(null);

            // Assert
            Assert.IsFalse(viewModel.EqLoggingIsOff);
        }

        [TestMethod]
        public void EqLoggingIsOff_IsRecheckedWhenTheDirectoryChanges()
        {
            // Arrange
            // Pointing the client at an install is exactly when this becomes
            // knowable, so it has to be looked at again rather than only once at
            // construction.
            WriteIni("Log=FALSE");
            var viewModel = CreateViewModel(null);
            Assert.IsFalse(viewModel.EqLoggingIsOff);

            // Act
            viewModel.EqDirectory = eqDirectory;

            // Assert
            Assert.IsTrue(viewModel.EqLoggingIsOff);
        }

        [TestMethod]
        public void EqLoggingIsOff_ReadsTheSettingWhateverItsSpacingAndCase()
        {
            // Arrange
            // The file is hand-edited often enough that this is worth pinning.
            WriteIni("  log = false  ");

            // Act
            var viewModel = CreateViewModel(eqDirectory);

            // Assert
            Assert.IsTrue(viewModel.EqLoggingIsOff);
        }
    }
}
