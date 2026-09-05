using Avalonia;
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
    // Nothing in this client remembered where a window was. WindowState carries a
    // WindowRect and no code read or wrote it, because upstream saves that from a
    // WPF base class that is not part of this build. An overlay dragged over the
    // game went back to its default on the next launch.
    [TestClass]
    public class WindowGeometryTests
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

        private static EventOverlayWindow NewOverlay(EQToolSettings settings, out EventOverlayViewModel viewModel)
        {
            viewModel = new EventOverlayViewModel(new LogEvents());
            return new EventOverlayWindow(viewModel, settings);
        }

        [TestMethod]
        public void Capture_RecordsWhereTheWindowIs()
        {
            OnUiThread(() =>
            {
                // Arrange
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var overlay = NewOverlay(settings, out var viewModel);
                overlay.Show();
                overlay.Position = new PixelPoint(321, 654);

                // Act
                WindowPreferences.Capture(overlay, settings.OverlayWindowState);

                // Assert
                var rect = settings.OverlayWindowState.WindowRect;
                Assert.IsTrue(rect.HasValue, "Nothing was recorded.");
                Assert.AreEqual(321, (int)rect.Value.X);
                Assert.AreEqual(654, (int)rect.Value.Y);

                overlay.Close();
                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void ClosingAWindow_RecordsItsPositionWithoutBeingAsked()
        {
            OnUiThread(() =>
            {
                // Arrange
                // Attach is what every window calls, so closing has to be enough.
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var overlay = NewOverlay(settings, out var viewModel);
                overlay.Show();
                overlay.Position = new PixelPoint(210, 120);

                // Act
                overlay.Close();

                // Assert
                var rect = settings.OverlayWindowState.WindowRect;
                Assert.IsTrue(rect.HasValue, "Closing did not record the position.");
                Assert.AreEqual(210, (int)rect.Value.X);

                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void Persist_IsNotRequired()
        {
            OnUiThread(() =>
            {
                // Arrange
                // Left null outside the running client, so a window in a test
                // records its geometry and writes nothing to disk.
                var previous = WindowPreferences.Persist;
                WindowPreferences.Persist = null;
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var overlay = NewOverlay(settings, out var viewModel);
                overlay.Show();

                // Act
                overlay.Close();

                // Assert
                Assert.IsTrue(settings.OverlayWindowState.WindowRect.HasValue);

                WindowPreferences.Persist = previous;
                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void Persist_IsCalledWhenAWindowCloses()
        {
            OnUiThread(() =>
            {
                // Arrange
                var previous = WindowPreferences.Persist;
                var saves = 0;
                WindowPreferences.Persist = () => saves++;
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var overlay = NewOverlay(settings, out var viewModel);
                overlay.Show();

                // Act
                overlay.Close();

                // Assert
                Assert.AreEqual(1, saves, "The recorded position was never written back.");

                WindowPreferences.Persist = previous;
                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void OpeningAWindow_MarksItAsNotClosed()
        {
            OnUiThread(() =>
            {
                // Arrange
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                settings.OverlayWindowState.Closed = true;
                var overlay = NewOverlay(settings, out var viewModel);

                // Act
                overlay.Show();

                // Assert
                Assert.IsFalse(settings.OverlayWindowState.Closed);

                overlay.Close();
                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void ClosingAWindow_MarksItClosed()
        {
            OnUiThread(() =>
            {
                // Arrange
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                var overlay = NewOverlay(settings, out var viewModel);
                overlay.Show();

                // Act
                overlay.Close();

                // Assert
                Assert.IsTrue(settings.OverlayWindowState.Closed);

                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void ShouldReopen_OnAFreshInstall_IsFalse()
        {
            // Arrange
            // Closed defaults to false for most of these upstream, so going by
            // that alone would open five windows at once for somebody who has
            // never run the client. Nothing was open, because there was no
            // previous session.
            var state = new EQTool.Models.WindowState { Closed = false };

            // Assert
            Assert.IsNull(state.WindowRect);
            Assert.IsFalse(WindowPreferences.ShouldReopen(state));
        }

        [TestMethod]
        public void ShouldReopen_AfterAWindowWasOpenAtExit_IsTrue()
        {
            // Arrange
            // Closing captures the geometry, so a stored rect is what separates a
            // window that was really open from one never seen.
            var state = new EQTool.Models.WindowState
            {
                Closed = false,
                WindowRect = new System.Windows.Rect(100, 120, 400, 300),
            };

            // Assert
            Assert.IsTrue(WindowPreferences.ShouldReopen(state));
        }

        [TestMethod]
        public void ShouldReopen_WhenItWasClosedByHand_IsFalse()
        {
            // Arrange
            var state = new EQTool.Models.WindowState
            {
                Closed = true,
                WindowRect = new System.Windows.Rect(100, 120, 400, 300),
            };

            // Assert
            Assert.IsFalse(WindowPreferences.ShouldReopen(state));
        }

        [TestMethod]
        public void ShouldReopen_WithNoState_IsFalse()
        {
            Assert.IsFalse(WindowPreferences.ShouldReopen(null));
        }

        [TestMethod]
        public void ARectOnAScreen_IsRestored()
        {
            OnUiThread(() =>
            {
                // Arrange
                // The counterpart to the off-screen case below. Without this the
                // guard is covered and the feature it guards is not: a Restore
                // that never applied anything would pass that test and fail this.
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                settings.OverlayWindowState.WindowRect =
                    new System.Windows.Rect(300, 200, 400, 300);

                var overlay = NewOverlay(settings, out var viewModel);

                // Act
                overlay.Show();

                // Assert
                Assert.AreEqual(300, overlay.Position.X);
                Assert.AreEqual(200, overlay.Position.Y);

                overlay.Close();
                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void ARectFarOffAnyScreen_IsNotRestored()
        {
            OnUiThread(() =>
            {
                // Arrange
                // A monitor that has since been unplugged would otherwise put the
                // window somewhere it cannot be reached or dragged back from.
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                settings.OverlayWindowState.WindowRect =
                    new System.Windows.Rect(-40000, -40000, 400, 300);

                var overlay = NewOverlay(settings, out var viewModel);

                // Act
                overlay.Show();

                // Assert
                Assert.AreNotEqual(-40000, overlay.Position.X, "The window was restored off every screen.");

                overlay.Close();
                viewModel.Dispose();
            });
        }

        [TestMethod]
        public void ASavedSize_IsRestored()
        {
            OnUiThread(() =>
            {
                // Arrange
                // Size does not depend on where the screens are, so it applies
                // whether or not the corner passes the screen check.
                var settings = new EQToolSettings { Triggers = new List<Trigger>() };
                settings.OverlayWindowState.WindowRect =
                    new System.Windows.Rect(50, 60, 517, 419);

                var overlay = NewOverlay(settings, out var viewModel);

                // Act
                overlay.Show();

                // Assert
                Assert.AreEqual(517, overlay.Width, 0.5);
                Assert.AreEqual(419, overlay.Height, 0.5);

                overlay.Close();
                viewModel.Dispose();
            });
        }
    }
}
