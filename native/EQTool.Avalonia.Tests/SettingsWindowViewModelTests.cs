using EQTool.Avalonia.ViewModels;
using EQTool.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace EQTool.Avalonia.Tests
{
    // The settings window has no Save button: every control persists on change.
    // These pin that, and the nullable defaults, because a setting that silently
    // fails to stick looks identical to one that works until the app restarts.
    [TestClass]
    public class SettingsWindowViewModelTests
    {
        private EQToolSettings settings;
        private RecordingTextToSpeach speech;
        private int saveCount;

        [TestInitialize]
        public void Setup()
        {
            settings = new EQToolSettings { Triggers = new List<Trigger>() };
            speech = new RecordingTextToSpeach();
            saveCount = 0;
        }

        private SettingsWindowViewModel CreateViewModel()
        {
            var triggerEditor = new TriggerEditorViewModel(
                settings,
                () => saveCount++,
                speech,
                new RecordingAudioService());

            return new SettingsWindowViewModel(settings, () => saveCount++, speech, triggerEditor);
        }

        [TestMethod]
        public void AudioVolume_Unset_DefaultsToFull()
        {
            // Arrange
            // GlobalAudioVolume is int?; an unset value must read as 100 rather
            // than as silence.
            settings.GlobalAudioVolume = null;

            // Act
            var viewModel = CreateViewModel();

            // Assert
            Assert.AreEqual(100, viewModel.AudioVolume);
        }

        [TestMethod]
        public void AudioVolume_Set_RoundTripsAndSaves()
        {
            // Arrange
            var viewModel = CreateViewModel();
            saveCount = 0;

            // Act
            viewModel.AudioVolume = 40;

            // Assert
            Assert.AreEqual(40, settings.GlobalAudioVolume);
            Assert.AreEqual(40, viewModel.AudioVolume);
            Assert.AreEqual(1, saveCount);
        }

        [TestMethod]
        public void FontSize_Unset_DefaultsToTwelve()
        {
            // Arrange
            settings.FontSize = null;

            // Act
            var viewModel = CreateViewModel();

            // Assert
            Assert.AreEqual(12, viewModel.FontSize);
        }

        [TestMethod]
        public void ShowRing8RollTime_Unset_DefaultsToOn()
        {
            // Arrange
            // This one is bool? and defaults true, which is why it renders checked
            // while the plain bools render unchecked.
            settings.ShowRing8RollTime = null;

            // Act
            var viewModel = CreateViewModel();

            // Assert
            Assert.IsTrue(viewModel.ShowRing8RollTime);
        }

        [TestMethod]
        public void ShowScoutRollTime_Unset_DefaultsToOn()
        {
            // Arrange
            settings.ShowScoutRollTime = null;

            // Act
            var viewModel = CreateViewModel();

            // Assert
            Assert.IsTrue(viewModel.ShowScoutRollTime);
        }

        [TestMethod]
        public void YouOnlySpells_Unset_DefaultsToOff()
        {
            // Act
            var viewModel = CreateViewModel();

            // Assert
            Assert.IsFalse(viewModel.YouOnlySpells);
        }

        [TestMethod]
        public void SelectedVoice_RoundTripsAndSaves()
        {
            // Arrange
            var viewModel = CreateViewModel();
            saveCount = 0;

            // Act
            viewModel.SelectedVoice = "Samantha";

            // Assert
            Assert.AreEqual("Samantha", settings.SelectedVoice);
            Assert.AreEqual(1, saveCount);
        }

        [TestMethod]
        public void EqLogDirectory_Set_ReportsItAsPresent()
        {
            // Arrange
            var viewModel = CreateViewModel();
            Assert.IsFalse(viewModel.HasEqLogDirectory);

            // Act
            viewModel.EqLogDirectory = "/Users/someone/EQ/Logs";

            // Assert
            Assert.IsTrue(viewModel.HasEqLogDirectory);
            Assert.AreEqual("/Users/someone/EQ/Logs", settings.EqLogDirectory);
        }

        [TestMethod]
        public void OverlayClickThrough_RoundTripsAndSaves()
        {
            // Arrange
            var viewModel = CreateViewModel();
            saveCount = 0;

            // Act
            viewModel.OverlayClickThrough = true;

            // Assert
            Assert.IsTrue(settings.OverlayClickThrough);
            Assert.AreEqual(1, saveCount);
        }

        [TestMethod]
        public void PreviewVoice_SpeaksASample()
        {
            // Arrange
            var viewModel = CreateViewModel();

            // Act
            viewModel.PreviewVoice();

            // Assert
            Assert.AreEqual(1, speech.Spoken.Count);
            Assert.IsFalse(string.IsNullOrWhiteSpace(speech.Spoken[0]));
        }

        [TestMethod]
        public void WindowPreferences_CoverEveryWindowTheAppCanOpen()
        {
            // Act
            var viewModel = CreateViewModel();

            // Assert
            CollectionAssert.AreEquivalent(
                new[] { "Timers", "Map", "DPS", "Mob Info", "Console", "Overlay" },
                viewModel.WindowPreferences.Select(a => a.Label).ToArray());
        }

        [TestMethod]
        public void WindowPreference_AlwaysOnTop_WritesThroughToTheWindowState()
        {
            // Arrange
            var viewModel = CreateViewModel();
            var map = viewModel.WindowPreferences.First(a => a.Label == "Map");
            saveCount = 0;

            // Act
            map.AlwaysOnTop = true;

            // Assert
            Assert.IsTrue(settings.MapWindowState.AlwaysOnTop);
            Assert.AreEqual(1, saveCount);
        }

        [TestMethod]
        public void WindowPreference_Opacity_WritesThroughToTheWindowState()
        {
            // Arrange
            var viewModel = CreateViewModel();
            var dps = viewModel.WindowPreferences.First(a => a.Label == "DPS");

            // Act
            dps.Opacity = 0.6;

            // Assert
            Assert.AreEqual(0.6, settings.DpsWindowState.Opacity);
        }

        [TestMethod]
        public void WindowPreference_UnchangedValue_DoesNotSaveAgain()
        {
            // Arrange
            // These fire from slider and checkbox bindings, which re-set the same
            // value freely; saving on every one would write the settings file
            // constantly while a slider is dragged.
            var viewModel = CreateViewModel();
            var console = viewModel.WindowPreferences.First(a => a.Label == "Console");
            console.AlwaysOnTop = true;
            saveCount = 0;

            // Act
            console.AlwaysOnTop = true;

            // Assert
            Assert.AreEqual(0, saveCount);
        }

        [TestMethod]
        public void WindowPreference_Opacity_Unset_ReadsAsFullyOpaque()
        {
            // Act
            var viewModel = CreateViewModel();

            // Assert
            Assert.IsTrue(viewModel.WindowPreferences.All(a => a.Opacity == 1.0));
        }
    }
}
