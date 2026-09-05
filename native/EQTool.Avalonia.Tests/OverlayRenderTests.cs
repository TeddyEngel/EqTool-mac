using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.ViewModels;
using EQTool.Avalonia.Views;
using EQTool.Models;
using EQTool.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;

namespace EQTool.Avalonia.Tests
{
    // Covers the parts of click-through that are not interop.
    //
    // setIgnoresMouseEvents needs a real NSWindow and cannot be reached here. But
    // the overlay passes clicks through transparent regions before that call is
    // ever involved, purely because its content is hit-test invisible, and that
    // is ordinary Avalonia state. So is the opacity clamp and Topmost.
    [TestClass]
    public class OverlayRenderTests
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

        private static T Run<T>(Func<EventOverlayWindow, T> read, EQToolSettings settings = null)
        {
            var result = default(T);
            var overlaySettings = settings ?? new EQToolSettings();

            session.Dispatch(() =>
            {
                var viewModel = new EventOverlayViewModel(new LogEvents());
                var window = new EventOverlayWindow(viewModel, overlaySettings);
                window.Show();
                window.UpdateLayout();

                result = read(window);

                window.Close();
                viewModel.Dispose();
            }, CancellationToken.None).GetAwaiter().GetResult();

            return result;
        }

        [TestMethod]
        public void Overlay_Shows_WithoutTouchingRealSettings()
        {
            // Act
            var visible = Run(window => window.IsVisible);

            // Assert
            Assert.IsTrue(visible);
        }

        [TestMethod]
        public void Overlay_HasNoWindowChrome()
        {
            // Act
            // A titlebar would sit over the game and could not be clicked through.
            var decorations = Run(window => window.SystemDecorations);

            // Assert
            Assert.AreEqual(SystemDecorations.None, decorations);
        }

        [TestMethod]
        public void Overlay_KeepsTheDragHandleClickable()
        {
            // Act
            // With click-through off, the handle is the only way to reposition the
            // overlay, so it must stay hit-test visible while the content does not.
            var handleIsClickable = Run(window =>
                window.GetVisualDescendants()
                    .OfType<Border>()
                    .Any(a => a.Name == "DragHandle" && a.IsHitTestVisible));

            // Assert
            Assert.IsTrue(handleIsClickable, "The drag handle must remain clickable.");
        }

        [TestMethod]
        public void Overlay_ContentPanelsArePassThrough()
        {
            // Act
            // This is what lets a click land on the game rather than the overlay
            // even before setIgnoresMouseEvents is applied.
            var passThroughCount = Run(window =>
                window.GetVisualDescendants()
                    .OfType<Control>()
                    .Count(a => !a.IsHitTestVisible));

            // Assert
            Assert.IsTrue(passThroughCount > 0, "Expected hit-test-invisible content in the overlay.");
        }

        [TestMethod]
        public void WindowPreferences_AppliesOpacityFromSettings()
        {
            // Arrange
            var state = new EQTool.Models.WindowState { Opacity = 0.4 };

            // Act
            var opacity = RunOnPlainWindow(window => { WindowPreferences.ApplyNow(window, state); return window.Opacity; });

            // Assert
            Assert.AreEqual(0.4, opacity, 0.001);
        }

        [TestMethod]
        public void WindowPreferences_ClampsAbsurdOpacityRatherThanHidingTheWindow()
        {
            // Arrange
            // A fully transparent window cannot be found again by the user, so the
            // floor matters more than fidelity to the stored value.
            var state = new EQTool.Models.WindowState { Opacity = 0.0 };

            // Act
            var opacity = RunOnPlainWindow(window => { WindowPreferences.ApplyNow(window, state); return window.Opacity; });

            // Assert
            Assert.AreEqual(0.1, opacity, 0.001);
        }

        [TestMethod]
        public void WindowPreferences_AppliesAlwaysOnTop()
        {
            // Arrange
            var state = new EQTool.Models.WindowState { AlwaysOnTop = true };

            // Act
            var topmost = RunOnPlainWindow(window => { WindowPreferences.ApplyNow(window, state); return window.Topmost; });

            // Assert
            Assert.IsTrue(topmost);
        }

        [TestMethod]
        public void WindowPreferences_NullState_LeavesTheWindowAlone()
        {
            // Act
            var opacity = RunOnPlainWindow(window =>
            {
                window.Opacity = 0.73;
                WindowPreferences.ApplyNow(window, null);
                return window.Opacity;
            });

            // Assert
            Assert.AreEqual(0.73, opacity, 0.001);
        }

        private static T RunOnPlainWindow<T>(Func<Window, T> act)
        {
            var result = default(T);

            session.Dispatch(() =>
            {
                var window = new Window { Width = 200, Height = 100 };
                window.Show();
                result = act(window);
                window.Close();
            }, CancellationToken.None).GetAwaiter().GetResult();

            return result;
        }
    }
}
