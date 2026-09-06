using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using EQTool.Avalonia.ViewModels;
using EQTool.Avalonia.Views;
using EQTool.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EQTool.Avalonia.Tests
{
    // Renders the real SettingsWindow through Avalonia's headless backend.
    //
    // This covers what was previously only reachable by screenshotting a running
    // app on an awake display: that the tabs exist, that the Triggers tab is one
    // of them, and that its list binds to real triggers.
    [TestClass]
    public class SettingsWindowRenderTests
    {
        private static HeadlessUnitTestSession session;

        [ClassInitialize]
        public static void StartSession(TestContext context)
        {
            session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp));
        }

        [ClassCleanup]
        public static void StopSession()
        {
            session?.Dispose();
        }

        private static SettingsWindowViewModel BuildViewModel()
        {
            var settings = new EQToolSettings
            {
                Triggers = new List<Trigger>
                {
                    new Trigger { TriggerName = "Enrage", SearchText = "has become ENRAGED", Category = "Combat", IsBuiltIn = true },
                    new Trigger { TriggerName = "Dragon Roar", SearchText = "You flee in terror", Category = "Encounters", IsBuiltIn = true },
                    new Trigger { TriggerName = "My Pull", SearchText = "pulling now", Category = "Default" }
                }
            };

            var triggerEditor = new TriggerEditorViewModel(
                settings,
                () => { },
                new RecordingTextToSpeach(),
                new RecordingAudioService());

            return new SettingsWindowViewModel(settings, () => { }, new RecordingTextToSpeach(), triggerEditor);
        }

        private static T Run<T>(System.Func<SettingsWindow, T> read)
        {
            var result = default(T);

            session.Dispatch(() =>
            {
                var window = new SettingsWindow(BuildViewModel());
                window.Show();
                result = read(window);
                window.Close();
            }, CancellationToken.None).GetAwaiter().GetResult();

            return result;
        }

        [TestMethod]
        public void SettingsWindow_Shows_WithoutTouchingRealSettings()
        {
            // Act
            var visible = Run(window => window.IsVisible);

            // Assert
            Assert.IsTrue(visible);
        }

        [TestMethod]
        public void SettingsWindow_HasTheFourExpectedTabs()
        {
            // Act
            var headers = Run(window => window
                .GetVisualDescendants()
                .OfType<TabControl>()
                .First()
                .Items
                .OfType<TabItem>()
                .Select(a => a.Header?.ToString())
                .ToList());

            // Assert
            CollectionAssert.AreEqual(
                new[] { "General", "Alerts", "Windows", "Triggers" },
                headers.ToArray());
        }

        [TestMethod]
        public void TriggersTab_ListBindsToTheTriggersInSettings()
        {
            // Act
            // Selecting the tab is what realises its content; a TabItem's body is
            // not built until it is shown.
            var names = Run(window =>
            {
                var tabs = window.GetVisualDescendants().OfType<TabControl>().First();
                tabs.SelectedIndex = 3;
                window.UpdateLayout();

                return window
                    .GetVisualDescendants()
                    .OfType<ListBox>()
                    .SelectMany(a => a.ItemsSource?.OfType<TriggerRowViewModel>() ?? Enumerable.Empty<TriggerRowViewModel>())
                    .Select(a => a.Name)
                    .ToList();
            });

            // Assert
            CollectionAssert.AreEquivalent(
                new[] { "Enrage", "My Pull", "Dragon Roar" },
                names.ToArray());
        }

        [TestMethod]
        public void TriggersTab_ShowsThePromptWhenNothingIsSelected()
        {
            // Act
            var promptVisible = Run(window =>
            {
                var tabs = window.GetVisualDescendants().OfType<TabControl>().First();
                tabs.SelectedIndex = 3;
                window.UpdateLayout();

                return window
                    .GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Any(a => a.IsVisible && (a.Text ?? string.Empty).Contains("Pick a trigger"));
            });

            // Assert
            Assert.IsTrue(promptVisible, "Expected the empty-selection prompt on the Triggers tab.");
        }

        [TestMethod]
        public void AlertsTab_OffersRealVoices()
        {
            // Act
            var voiceCount = Run(window =>
            {
                var tabs = window.GetVisualDescendants().OfType<TabControl>().First();
                tabs.SelectedIndex = 1;
                window.UpdateLayout();

                return window
                    .GetVisualDescendants()
                    .OfType<ComboBox>()
                    .Select(a => a.ItemsSource?.OfType<string>().Count() ?? 0)
                    .DefaultIfEmpty(0)
                    .Max();
            });

            // Assert
            Assert.IsTrue(voiceCount > 0, "Expected the voice list to be populated from say -v ?.");
        }

        [TestMethod]
        public void WindowsTab_ListsEveryWindowPreferenceRow()
        {
            // Act
            var labels = Run(window =>
            {
                var tabs = window.GetVisualDescendants().OfType<TabControl>().First();
                tabs.SelectedIndex = 2;
                window.UpdateLayout();

                return window
                    .GetVisualDescendants()
                    .OfType<ItemsControl>()
                    .SelectMany(a => a.ItemsSource?.OfType<WindowPreferenceViewModel>() ?? Enumerable.Empty<WindowPreferenceViewModel>())
                    .Select(a => a.Label)
                    .ToList();
            });

            // Assert
            CollectionAssert.AreEquivalent(
                new[] { "Timers", "Map", "DPS", "Mob Info", "Console", "Overlay" },
                labels.ToArray());
        }

        [TestMethod]
        public void GeneralTab_EnableLoggingButton_ShowsWhenLoggingIsDetectedOff()
        {
            // Arrange
            var eqDirectory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "pigparse-render-" + System.Guid.NewGuid().ToString("N"));
            _ = System.IO.Directory.CreateDirectory(eqDirectory);
            System.IO.File.WriteAllLines(
                System.IO.Path.Combine(eqDirectory, "eqclient.ini"),
                new[] { "[Defaults]", "Log=FALSE" });

            var settings = new EQToolSettings { Triggers = new List<Trigger>() };
            var triggerEditor = new TriggerEditorViewModel(
                settings, () => { }, new RecordingTextToSpeach(), new RecordingAudioService());
            var viewModel = new SettingsWindowViewModel(settings, () => { }, new RecordingTextToSpeach(), triggerEditor);

            var visibleBefore = true;
            var visibleAfter = false;
            var found = false;

            try
            {
                // Act
                session.Dispatch(() =>
                {
                    var window = new SettingsWindow(viewModel);
                    window.Show();
                    window.UpdateLayout();

                    var button = window
                        .GetVisualDescendants()
                        .OfType<Button>()
                        .FirstOrDefault(a => (a.Content as string) == "Turn EverQuest logging on");

                    found = button != null;
                    if (button != null)
                    {
                        visibleBefore = button.IsVisible;

                        settings.DefaultEqDirectory = eqDirectory;
                        viewModel.RefreshLoggingState();
                        window.UpdateLayout();

                        visibleAfter = button.IsVisible;
                    }

                    window.Close();
                }, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                Assert.IsTrue(found, "Expected a logging button on the General tab.");
                Assert.IsFalse(visibleBefore, "It should not be showing before a directory is set.");
                Assert.IsTrue(visibleAfter, "It should show once a directory with Log=FALSE is detected.");
            }
            finally
            {
                System.IO.Directory.Delete(eqDirectory, true);
            }
        }

        [TestMethod]
        public void GeneralTab_ResetTriggersButton_TracksTheArmedFlag()
        {
            // Arrange
            var settings = new EQToolSettings
            {
                Triggers = new List<Trigger>
                {
                    new Trigger { TriggerName = "My Pull", SearchText = "pulling now", Category = "Default" }
                }
            };
            var triggerEditor = new TriggerEditorViewModel(
                settings,
                () => { },
                new RecordingTextToSpeach(),
                new RecordingAudioService());
            var viewModel = new SettingsWindowViewModel(settings, () => { }, new RecordingTextToSpeach(), triggerEditor);

            var found = false;
            var enabledBeforeArming = true;
            var enabledAfterArming = false;

            // Act
            session.Dispatch(() =>
            {
                var window = new SettingsWindow(viewModel);
                window.Show();
                window.UpdateLayout();

                var button = window
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .FirstOrDefault(a => (a.Content as string) == "Reset Triggers");

                found = button != null;
                if (button != null)
                {
                    enabledBeforeArming = button.IsEnabled;
                    triggerEditor.ResetArmed = true;
                    window.UpdateLayout();
                    enabledAfterArming = button.IsEnabled;
                }

                window.Close();
            }, CancellationToken.None).GetAwaiter().GetResult();

            // Assert
            Assert.IsTrue(found, "Expected a Reset Triggers button on the General tab.");
            Assert.IsFalse(enabledBeforeArming, "Reset should stay disabled until it is armed.");
            Assert.IsTrue(enabledAfterArming, "Arming the reset should enable the button.");
        }
    }
}
