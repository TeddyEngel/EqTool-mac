using Avalonia.Headless;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.ViewModels;
using EQTool.Avalonia.Views;
using EQTool.Models;
using EQTool.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
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
