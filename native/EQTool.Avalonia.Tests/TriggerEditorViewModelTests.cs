using EQTool.Avalonia.ViewModels;
using EQTool.Models;
using EQTool.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace EQTool.Avalonia.Tests
{
    public class RecordingTextToSpeach : ITextToSpeach
    {
        public List<string> Spoken { get; } = new List<string>();

        public void Say(string text) => Spoken.Add(text);
    }

    public class RecordingAudioService : IAudioService
    {
        public List<string> Played { get; } = new List<string>();

        public void Play(string soundFilePath) => Played.Add(soundFilePath);
    }

    [TestClass]
    public class TriggerEditorViewModelTests
    {
        private EQToolSettings settings;
        private RecordingTextToSpeach speech;
        private RecordingAudioService audio;

        [TestInitialize]
        public void Setup()
        {
            settings = new EQToolSettings();
            speech = new RecordingTextToSpeach();
            audio = new RecordingAudioService();

            settings.Triggers = new List<Trigger>
            {
                new Trigger
                {
                    TriggerName = "Enrage",
                    SearchText = "has become ENRAGED",
                    Category = "Combat",
                    TriggerEnabled = true,
                    IsBuiltIn = true
                },
                new Trigger
                {
                    TriggerName = "Dragon Roar",
                    SearchText = "You flee in terror",
                    Category = "Encounters",
                    TriggerEnabled = false,
                    IsBuiltIn = true
                },
                new Trigger
                {
                    TriggerName = "My Custom Pull",
                    SearchText = "pulling now",
                    Category = "Default",
                    TriggerEnabled = true,
                    IsBuiltIn = false
                }
            };
        }

        private int saveCount;

        private TriggerEditorViewModel CreateEditor()
        {
            return new TriggerEditorViewModel(settings, () => saveCount++, speech, audio);
        }

        [TestMethod]
        public void Triggers_WithNoFilter_ListsEveryTrigger()
        {
            // Act
            var editor = CreateEditor();

            // Assert
            Assert.AreEqual(3, editor.Triggers.Count);
        }

        [TestMethod]
        public void Triggers_AreOrderedByCategoryThenName()
        {
            // Act
            var editor = CreateEditor();

            // Assert
            CollectionAssert.AreEqual(
                new[] { "Enrage", "My Custom Pull", "Dragon Roar" },
                editor.Triggers.Select(a => a.Name).ToArray());
        }

        [TestMethod]
        public void FilterText_MatchingName_NarrowsTheList()
        {
            // Arrange
            var editor = CreateEditor();

            // Act
            editor.FilterText = "dragon";

            // Assert
            Assert.AreEqual(1, editor.Triggers.Count);
            Assert.AreEqual("Dragon Roar", editor.Triggers[0].Name);
        }

        [TestMethod]
        public void FilterText_MatchingSearchTextRatherThanName_StillFinds()
        {
            // Arrange
            // The thing a trigger matches on is often more memorable than what it
            // was named, so the filter looks at both.
            var editor = CreateEditor();

            // Act
            editor.FilterText = "ENRAGED";

            // Assert
            Assert.AreEqual(1, editor.Triggers.Count);
            Assert.AreEqual("Enrage", editor.Triggers[0].Name);
        }

        [TestMethod]
        public void Badge_ReflectsBuiltInAndCustomised()
        {
            // Arrange
            var editor = CreateEditor();

            // Act
            var builtIn = editor.Triggers.First(a => a.Name == "Enrage");
            var custom = editor.Triggers.First(a => a.Name == "My Custom Pull");

            // Assert
            Assert.AreEqual("built in", builtIn.Badge);
            Assert.AreEqual("custom", custom.Badge);
        }

        [TestMethod]
        public void SelectingATrigger_ExposesItsFieldsForEditing()
        {
            // Arrange
            var editor = CreateEditor();

            // Act
            editor.Selected = editor.Triggers.First(a => a.Name == "Dragon Roar");

            // Assert
            Assert.IsTrue(editor.HasSelection);
            Assert.AreEqual("Dragon Roar", editor.TriggerName);
            Assert.AreEqual("You flee in terror", editor.SearchText);
        }

        [TestMethod]
        public void NoSelection_ReportsNoFieldsRatherThanThrowing()
        {
            // Act
            var editor = CreateEditor();

            // Assert
            Assert.IsFalse(editor.HasSelection);
            Assert.IsNull(editor.TriggerName);
            Assert.IsNull(editor.SearchText);
            Assert.AreEqual(TriggerAudioType.None, editor.AudioType);
            Assert.AreEqual(TimerType.NoTimer, editor.TimerType);
        }

        [TestMethod]
        public void AudioType_TextToSpeech_ShowsOnlyTheSpokenTextField()
        {
            // Arrange
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();

            // Act
            editor.AudioType = TriggerAudioType.TextToSpeech;

            // Assert
            Assert.IsTrue(editor.IsTextToSpeech);
            Assert.IsFalse(editor.IsSoundFile);
        }

        [TestMethod]
        public void AudioType_SoundFile_ShowsOnlyTheSoundFileField()
        {
            // Arrange
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();

            // Act
            editor.AudioType = TriggerAudioType.SoundFile;

            // Assert
            Assert.IsTrue(editor.IsSoundFile);
            Assert.IsFalse(editor.IsTextToSpeech);
        }

        [TestMethod]
        public void TimerType_NoTimer_HidesTheDurationFields()
        {
            // Arrange
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();

            // Act
            editor.TimerType = TimerType.NoTimer;

            // Assert
            Assert.IsFalse(editor.HasTimer);
        }

        [TestMethod]
        public void TimerType_CountDown_ShowsTheDurationFields()
        {
            // Arrange
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();

            // Act
            editor.TimerType = TimerType.CountDown;

            // Assert
            Assert.IsTrue(editor.HasTimer);
        }

        [TestMethod]
        public void EditingABuiltInTrigger_MarksItCustomised()
        {
            // Arrange
            // Upstream sets the same flag, because an edited built-in stops
            // receiving upstream's later fixes for that trigger.
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First(a => a.Name == "Enrage");
            Assert.IsFalse(editor.Selected.Source.Customized);

            // Act
            editor.DisplayText = "Enrage! Back off.";

            // Assert
            Assert.IsTrue(editor.Selected.Source.Customized);
        }

        [TestMethod]
        public void EditingACustomTrigger_DoesNotSetTheBuiltInFlag()
        {
            // Arrange
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First(a => a.Name == "My Custom Pull");

            // Act
            editor.DisplayText = "Pulling";

            // Assert
            Assert.IsFalse(editor.Selected.Source.IsBuiltIn);
            Assert.IsFalse(editor.Selected.Source.Customized);
        }

        [TestMethod]
        public void PreviewOutput_TextToSpeech_SpeaksTheConfiguredText()
        {
            // Arrange
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();
            editor.AudioType = TriggerAudioType.TextToSpeech;
            editor.TtsText = "Dragon Roar incoming";

            // Act
            editor.PreviewOutput();

            // Assert
            CollectionAssert.AreEqual(new[] { "Dragon Roar incoming" }, speech.Spoken.ToArray());
            Assert.AreEqual(0, audio.Played.Count);
        }

        [TestMethod]
        public void PreviewOutput_SoundFile_PlaysTheConfiguredFile()
        {
            // Arrange
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();
            editor.AudioType = TriggerAudioType.SoundFile;
            editor.SoundFile = "/tmp/alert.wav";

            // Act
            editor.PreviewOutput();

            // Assert
            CollectionAssert.AreEqual(new[] { "/tmp/alert.wav" }, audio.Played.ToArray());
            Assert.AreEqual(0, speech.Spoken.Count);
        }

        [TestMethod]
        public void PreviewOutput_NoAlertConfigured_DoesNothing()
        {
            // Arrange
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();
            editor.AudioType = TriggerAudioType.None;

            // Act
            editor.PreviewOutput();

            // Assert
            Assert.AreEqual(0, speech.Spoken.Count);
            Assert.AreEqual(0, audio.Played.Count);
        }

        [TestMethod]
        public void TogglingEnabled_WritesThroughToTheTrigger()
        {
            // Arrange
            var editor = CreateEditor();
            var row = editor.Triggers.First(a => a.Name == "Dragon Roar");
            Assert.IsFalse(row.Source.TriggerEnabled);

            // Act
            row.Enabled = true;

            // Assert
            Assert.IsTrue(row.Source.TriggerEnabled);
        }

        [TestMethod]
        public void EditingAField_SavesImmediately()
        {
            // Arrange
            // The settings window has no Save button; upstream persists on every
            // change and this editor has to match or edits are silently lost.
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();
            saveCount = 0;

            // Act
            editor.DisplayText = "Back off";

            // Assert
            Assert.AreEqual(1, saveCount);
        }

        [TestMethod]
        public void TogglingEnabled_SavesImmediately()
        {
            // Arrange
            var editor = CreateEditor();
            var row = editor.Triggers.First(a => a.Name == "Dragon Roar");
            saveCount = 0;

            // Act
            row.Enabled = true;

            // Assert
            Assert.AreEqual(1, saveCount);
        }

        [TestMethod]
        public void SettingAFieldToItsCurrentValue_StillSavesRatherThanSilentlySkipping()
        {
            // Arrange
            // Worth pinning: the enabled toggle short-circuits on an unchanged
            // value, the text fields deliberately do not, and a future "optimisation"
            // that made them match would be a behaviour change.
            var editor = CreateEditor();
            editor.Selected = editor.Triggers.First();
            editor.DisplayText = "Back off";
            saveCount = 0;

            // Act
            editor.DisplayText = "Back off";

            // Assert
            Assert.AreEqual(1, saveCount);
        }
    }
}