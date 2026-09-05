using Avalonia.Headless;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.ViewModels;
using EQTool.Avalonia.Views;
using EQTool.Models;
using EQTool.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EQTool.Avalonia.Tests
{
    // The overlay only reads OverlayClickThrough when it opens. Changing the
    // setting therefore did nothing to an overlay already on screen, which is
    // worst when turning click-through off: that is the only way to get the drag
    // handle back, so the overlay was stuck where it was until it was reopened.
    //
    // These cover the registry lookup and the guards. The AppKit half cannot run
    // here, since a headless window has no NSWindow behind it and the interop
    // declines rather than throwing. That part was checked by driving the real
    // window and reading ignoresMouseEvents back; see PORT-NOTES.
    [TestClass]
    public class WindowManagerTests
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

        private static void OnUiThread(System.Action body)
        {
            session.Dispatch(body, CancellationToken.None).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void TryGet_WithNothingOpen_ReturnsFalse()
        {
            OnUiThread(() =>
            {
                // Act
                var found = WindowManager.TryGet<EventOverlayWindow>(out var overlay);

                // Assert
                Assert.IsFalse(found);
                Assert.IsNull(overlay);
            });
        }

        [TestMethod]
        public void TryGet_AfterAdopt_FindsTheSameInstance()
        {
            OnUiThread(() =>
            {
                // Arrange
                var viewModel = new EventOverlayViewModel(new LogEvents());
                var overlay = new EventOverlayWindow(viewModel, new EQToolSettings());
                overlay.Show();
                WindowManager.Adopt(overlay);

                // Act
                var found = WindowManager.TryGet<EventOverlayWindow>(out var resolved);

                // Assert
                Assert.IsTrue(found);
                Assert.AreSame(overlay, resolved);

                overlay.Close();
                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void TryGet_AfterTheWindowCloses_ForgetsIt()
        {
            OnUiThread(() =>
            {
                // Arrange
                // A stale entry would leave the settings toggle writing to a dead
                // window and silently doing nothing.
                var viewModel = new EventOverlayViewModel(new LogEvents());
                var overlay = new EventOverlayWindow(viewModel, new EQToolSettings());
                overlay.Show();
                WindowManager.Adopt(overlay);

                // Act
                overlay.Close();
                viewModel.Dispose();

                // Assert
                Assert.IsFalse(WindowManager.TryGet<EventOverlayWindow>(out _));
            });
        }

        [TestMethod]
        public void ApplyOverlayClickThrough_WithNoOverlayOpen_DoesNothing()
        {
            OnUiThread(() =>
            {
                // Act
                // The settings window can be open without the overlay being open.
                WindowManager.ApplyOverlayClickThrough(true);

                // Assert
                Assert.IsFalse(WindowManager.TryGet<EventOverlayWindow>(out _));
            });
        }

        private static WindowPreferenceViewModel OverlayRow(SettingsWindowViewModel viewModel)
        {
            return viewModel.WindowPreferences.Single(row => row.Label == "Overlay");
        }

        private static SettingsWindowViewModel BuildSettingsViewModel(EQToolSettings settings)
        {
            var speech = new RecordingTextToSpeach();
            var triggerEditor = new TriggerEditorViewModel(
                settings, () => { }, speech, new RecordingAudioService());
            return new SettingsWindowViewModel(settings, () => { }, speech, triggerEditor);
        }

        [TestMethod]
        public void FontSize_Changed_RescalesTheApplicationTokens()
        {
            OnUiThread(() =>
            {
                // Arrange
                // The slider wrote to settings and nothing read it, for the whole
                // life of the client.
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var viewModel = BuildSettingsViewModel(settings);

                // Act
                viewModel.FontSize = 24;

                // Assert
                Assert.AreEqual(24.0, (double)global::Avalonia.Application.Current.Resources["TypeBody"], 0.001);
                Assert.AreEqual(40.0, (double)global::Avalonia.Application.Current.Resources["TypeTitle"], 0.001);

                // Put it back so the other tests see the design values.
                viewModel.FontSize = 12;
                Assert.AreEqual(12.0, (double)global::Avalonia.Application.Current.Resources["TypeBody"], 0.001);
            });
        }

        [TestMethod]
        public void Opacity_ChangedWhileOpen_AppliesToTheWindow()
        {
            OnUiThread(() =>
            {
                // Arrange
                // Opacity is the clearest case of the bug this covers: the slider
                // moves, and the window it names does not change until reopened.
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var overlayViewModel = new EventOverlayViewModel(new LogEvents());
                var overlay = new EventOverlayWindow(overlayViewModel, settings);
                overlay.Show();
                WindowManager.Adopt(overlay);
                var viewModel = BuildSettingsViewModel(settings);

                // Act
                OverlayRow(viewModel).Opacity = 0.5;

                // Assert
                Assert.AreEqual(0.5, overlay.Opacity, 0.001);

                overlay.Close();
                overlayViewModel.Dispose();
            });
        }

        [TestMethod]
        public void AlwaysOnTop_ChangedWhileOpen_AppliesToTheWindow()
        {
            OnUiThread(() =>
            {
                // Arrange
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var overlayViewModel = new EventOverlayViewModel(new LogEvents());
                var overlay = new EventOverlayWindow(overlayViewModel, settings);
                overlay.Show();
                WindowManager.Adopt(overlay);
                var viewModel = BuildSettingsViewModel(settings);
                var row = OverlayRow(viewModel);

                // Attaching already applied the stored value, so assert the
                // direction that has to change. Asserting the other way passes
                // whether or not the setting is ever re-applied.
                Assert.IsFalse(overlay.Topmost);

                // Act
                row.AlwaysOnTop = true;

                // Assert
                Assert.IsTrue(overlay.Topmost);

                overlay.Close();
                overlayViewModel.Dispose();
            });
        }

        [TestMethod]
        public void Opacity_AppliedWhileOpen_KeepsTheFloor()
        {
            OnUiThread(() =>
            {
                // Arrange
                // Applying live must clamp exactly as opening does, or the window
                // can be made invisible and then cannot be found to fix it.
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var overlayViewModel = new EventOverlayViewModel(new LogEvents());
                var overlay = new EventOverlayWindow(overlayViewModel, settings);
                overlay.Show();
                WindowManager.Adopt(overlay);
                var viewModel = BuildSettingsViewModel(settings);

                // Act
                OverlayRow(viewModel).Opacity = 0.0;

                // Assert
                Assert.AreEqual(0.1, overlay.Opacity, 0.001);

                overlay.Close();
                overlayViewModel.Dispose();
            });
        }

        [TestMethod]
        public void SettingsToggle_ReachesAnOpenOverlay()
        {
            OnUiThread(() =>
            {
                // Arrange
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var speech = new RecordingTextToSpeach();
                var triggerEditor = new TriggerEditorViewModel(
                    settings, () => { }, speech, new RecordingAudioService());
                var viewModel = new SettingsWindowViewModel(settings, () => { }, speech, triggerEditor);

                var overlayViewModel = new EventOverlayViewModel(new LogEvents());
                var overlay = new EventOverlayWindow(overlayViewModel, settings);
                overlay.Show();
                WindowManager.Adopt(overlay);

                // Act
                // Before the fix this reached the settings object and stopped
                // there, leaving the open overlay on its old value.
                viewModel.OverlayClickThrough = true;

                // Assert
                Assert.IsTrue(settings.OverlayClickThrough);
                Assert.IsTrue(
                    WindowPreferences.TryGetRequestedClickThrough(overlay, out var applied),
                    "The toggle never reached the open overlay.");
                Assert.IsTrue(applied);

                // And back off again, which is the case that matters: it is the
                // only way to get the drag handle back and move the overlay.
                viewModel.OverlayClickThrough = false;
                Assert.IsTrue(WindowPreferences.TryGetRequestedClickThrough(overlay, out var reverted));
                Assert.IsFalse(reverted);

                overlay.Close();
                overlayViewModel.Dispose();
            });
        }
    }
}
