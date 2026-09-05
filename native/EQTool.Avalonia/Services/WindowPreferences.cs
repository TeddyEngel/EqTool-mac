using Avalonia.Controls;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace EQTool.Avalonia.Services
{
    // Applies the per-window settings to a real window.
    //
    // Without this the Windows tab writes always-on-top and opacity into
    // settings.json and nothing reads them back, so the controls look functional
    // and do nothing.
    //
    // Interop calls need the NSWindow, which does not exist until the window is
    // opened, so Attach defers to the Opened event rather than running in a
    // constructor where the handle would still be null.
    public static class WindowPreferences
    {
        public static void Attach(Window window, EQTool.Models.WindowState state, bool asOverlay = false)
        {
            if (window == null || state == null)
                return;

            void Apply(object sender, EventArgs e) => ApplyNow(window, state, asOverlay);

            if (window.IsLoaded)
                ApplyNow(window, state, asOverlay);

            window.Opened += Apply;
            window.Closed += (_, _) => window.Opened -= Apply;
        }

        [SupportedOSPlatform("macos")]
        private static void ApplyPlatformWindowLevel(Window window, bool alwaysOnTop, bool asOverlay)
        {
            if (asOverlay)
            {
                MacOSWindowInterop.SetWindowLevel(window, MacOSWindowInterop.OverlayWindowLevel);
                MacOSWindowInterop.MakeOverlayBehaviour(window);
                return;
            }

            MacOSWindowInterop.SetWindowLevel(
                window,
                alwaysOnTop ? MacOSWindowInterop.NSStatusWindowLevel : MacOSWindowInterop.NSNormalWindowLevel);
        }

        public static void ApplyNow(Window window, EQTool.Models.WindowState state, bool asOverlay = false)
        {
            if (window == null || state == null)
                return;

            window.Opacity = Math.Clamp(state.Opacity ?? 1.0, 0.1, 1.0);
            window.Topmost = state.AlwaysOnTop;

            if (!OperatingSystem.IsMacOS())
                return;

            // Avalonia's Topmost is NSFloatingWindowLevel (3), which sits below a
            // Wine fullscreen window at 26, so an overlay is raised past it.
            ApplyPlatformWindowLevel(window, state.AlwaysOnTop, asOverlay);
        }

        [SupportedOSPlatform("macos")]
        private static void SetClickThroughCore(Window window, bool clickThrough)
        {
            MacOSWindowInterop.SetIgnoresMouseEvents(window, clickThrough);
        }

        // What was last asked for, per window. The interop leaves no readable
        // trace off macOS, and none at all without an NSWindow behind the window,
        // so this is the only way to tell whether a caller reached this method.
        private static readonly ConditionalWeakTable<Window, object> RequestedClickThrough =
            new ConditionalWeakTable<Window, object>();

        internal static bool TryGetRequestedClickThrough(Window window, out bool clickThrough)
        {
            if (window != null && RequestedClickThrough.TryGetValue(window, out var stored))
            {
                clickThrough = (bool)stored;
                return true;
            }

            clickThrough = false;
            return false;
        }

        public static void SetClickThrough(Window window, bool clickThrough)
        {
            if (window == null)
                return;

            RequestedClickThrough.Remove(window);
            RequestedClickThrough.Add(window, clickThrough);

            if (!OperatingSystem.IsMacOS())
                return;

            SetClickThroughCore(window, clickThrough);
        }
    }
}
