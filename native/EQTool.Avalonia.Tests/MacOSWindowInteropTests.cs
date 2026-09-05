using Avalonia.Controls;
using Avalonia.Headless;
using EQTool.Avalonia.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace EQTool.Avalonia.Tests
{
    // Checks the interop against the real Objective-C runtime.
    //
    // Actually observing click-through needs a window on a screen. What can be
    // checked without one is the part most likely to be silently wrong: whether
    // the selectors exist on NSWindow at all.
    //
    // sel_registerName registers an unknown selector rather than failing, so a
    // misspelling produces a live selector that no class responds to. The call
    // then does nothing, which looks exactly like the platform not supporting the
    // feature. class_getInstanceMethod is the check that catches it.
    [TestClass]
    public class MacOSWindowInteropTests
    {
        private const string LibObjC = "/usr/lib/libobjc.A.dylib";

        [DllImport(LibObjC)]
        private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibObjC)]
        private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibObjC)]
        private static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr selector);

        [DllImport("/usr/lib/libSystem.dylib")]
        private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string path, int mode);

        private const int RtldLazy = 1;

        // A .NET test host has libobjc but has not loaded AppKit, so NSWindow is
        // not registered with the runtime until the framework is pulled in. The
        // real app gets this for free by being a GUI process.
        [ClassInitialize]
        public static void LoadAppKit(TestContext context)
        {
            _ = dlopen("/System/Library/Frameworks/AppKit.framework/AppKit", RtldLazy);
        }

        private static bool NSWindowRespondsTo(string selectorName)
        {
            var nsWindow = objc_getClass("NSWindow");
            Assert.AreNotEqual(IntPtr.Zero, nsWindow, "NSWindow class not found in the Objective-C runtime.");

            var selector = sel_registerName(selectorName);
            return class_getInstanceMethod(nsWindow, selector) != IntPtr.Zero;
        }

        [TestMethod]
        public void SetIgnoresMouseEvents_IsARealSelectorOnNSWindow()
        {
            // This is the selector the whole click-through feature rests on.
            Assert.IsTrue(
                NSWindowRespondsTo("setIgnoresMouseEvents:"),
                "NSWindow has no setIgnoresMouseEvents: - click-through would silently do nothing.");
        }

        [TestMethod]
        public void SetLevel_IsARealSelectorOnNSWindow()
        {
            Assert.IsTrue(
                NSWindowRespondsTo("setLevel:"),
                "NSWindow has no setLevel: - the overlay could not be raised above Wine.");
        }

        [TestMethod]
        public void CollectionBehaviorSelectors_AreRealOnNSWindow()
        {
            Assert.IsTrue(NSWindowRespondsTo("setCollectionBehavior:"));
            Assert.IsTrue(NSWindowRespondsTo("collectionBehavior"));
        }

        [TestMethod]
        public void AMisspeltSelector_IsNotRejectedByRegistration()
        {
            // Demonstrates why the tests above are worth having: registering
            // nonsense succeeds, so a typo cannot be caught at the call site.
            var bogus = sel_registerName("setIgnoresMouseEventsTypo:");

            Assert.AreNotEqual(IntPtr.Zero, bogus, "sel_registerName rejected an unknown selector.");
            Assert.IsFalse(
                NSWindowRespondsTo("setIgnoresMouseEventsTypo:"),
                "A misspelt selector must not resolve to a real NSWindow method.");
        }

        [TestMethod]
        public void OverlayWindowLevel_ClearsWineFullscreen()
        {
            // Wine puts a fullscreen window at NSStatusWindowLevel + 1 = 26, so an
            // overlay meant to cover the game has to exceed that.
            Assert.IsTrue(
                MacOSWindowInterop.OverlayWindowLevel > MacOSWindowInterop.NSStatusWindowLevel + 1,
                "Overlay level " + MacOSWindowInterop.OverlayWindowLevel + " does not clear Wine's fullscreen level of 26.");
        }

        [TestMethod]
        public void WindowLevelConstants_MatchAppKit()
        {
            // Pinned so a future tidy-up cannot quietly renumber them.
            Assert.AreEqual(0, MacOSWindowInterop.NSNormalWindowLevel);
            Assert.AreEqual(3, MacOSWindowInterop.NSFloatingWindowLevel);
            Assert.AreEqual(25, MacOSWindowInterop.NSStatusWindowLevel);
        }

        [TestMethod]
        public void CollectionBehaviourFlags_MatchAppKitBitPositions()
        {
            Assert.AreEqual(1UL << 0, MacOSWindowInterop.NSWindowCollectionBehaviorCanJoinAllSpaces);
            Assert.AreEqual(1UL << 4, MacOSWindowInterop.NSWindowCollectionBehaviorStationary);
            Assert.AreEqual(1UL << 7, MacOSWindowInterop.NSWindowCollectionBehaviorFullScreenPrimary);
            Assert.AreEqual(1UL << 8, MacOSWindowInterop.NSWindowCollectionBehaviorFullScreenAuxiliary);
        }
    }

    [TestClass]
    public class MacOSWindowInteropGuardTests
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

        // A headless window has no NSWindow behind it, which is exactly the
        // condition the guard exists for: the interop must decline rather than
        // dereference whatever handle it is given.
        [TestMethod]
        public void InteropCalls_OnAWindowWithNoNSWindow_DoNotThrow()
        {
            var threw = false;

            session.Dispatch(() =>
            {
                var window = new Window { Width = 100, Height = 100 };
                window.Show();

                try
                {
                    MacOSWindowInterop.SetIgnoresMouseEvents(window, true);
                    MacOSWindowInterop.SetWindowLevel(window, MacOSWindowInterop.OverlayWindowLevel);
                    MacOSWindowInterop.MakeOverlayBehaviour(window);
                }
                catch (Exception)
                {
                    threw = true;
                }

                window.Close();
            }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(threw, "Interop must no-op when there is no NSWindow, not throw.");
        }

        [TestMethod]
        public void InteropCalls_OnANullWindow_DoNotThrow()
        {
            var threw = false;

            try
            {
                MacOSWindowInterop.SetIgnoresMouseEvents(null, true);
                MacOSWindowInterop.SetWindowLevel(null, 0);
                MacOSWindowInterop.MakeOverlayBehaviour(null);
            }
            catch (Exception)
            {
                threw = true;
            }

            Assert.IsFalse(threw);
        }
    }
}
