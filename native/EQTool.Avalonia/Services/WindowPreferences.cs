using Avalonia;
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
        // Installed by the shell at startup and left null everywhere else, so a
        // window built in a test records its geometry without writing to disk.
        // Same shape as DispatcherTimer.Host.
        public static Action Persist { get; set; }

        public static void Attach(
            Window window,
            EQTool.Models.WindowState state,
            bool asOverlay = false)
        {
            if (window == null || state == null)
                return;

            void Apply(object sender, EventArgs e)
            {
                state.Closed = false;
                ApplyNow(window, state, asOverlay);
            }

            void Remember(object sender, WindowClosingEventArgs e)
            {
                state.Closed = true;
                Capture(window, state);
                Persist?.Invoke();
            }

            if (window.IsLoaded)
                ApplyNow(window, state, asOverlay);

            window.Opened += Apply;
            window.Closing += Remember;
            window.Closed += (_, _) =>
            {
                window.Opened -= Apply;
                window.Closing -= Remember;
            };
        }

        // Matches upstream, which reopens on Closed alone. Closed is false on a
        // fresh install, so a first run opens the same set the Windows client
        // opens rather than nothing.
        public static bool ShouldReopen(EQTool.Models.WindowState state)
        {
            return state != null && !state.Closed;
        }

        // Upstream saves this from a WPF base class that is not part of this
        // build, so nothing here remembered a position.
        public static void Capture(Window window, EQTool.Models.WindowState state)
        {
            if (window == null || state == null)
                return;

            var position = window.Position;
            state.WindowRect = new System.Windows.Rect(
                position.X,
                position.Y,
                window.Width,
                window.Height);
        }

        private static bool IsOnAScreen(Window window, PixelPoint point)
        {
            var screens = window.Screens;
            if (screens == null)
                return false;

            foreach (var screen in screens.All)
            {
                if (screen.Bounds.Contains(point))
                    return true;
            }

            return false;
        }

        private static void Restore(Window window, EQTool.Models.WindowState state)
        {
            var rect = state.WindowRect;
            if (!rect.HasValue)
                return;

            if (rect.Value.Width > 0)
                window.Width = rect.Value.Width;

            if (rect.Value.Height > 0)
                window.Height = rect.Value.Height;

            // Corner only: Position is physical pixels and the size is device
            // independent, so adding them would be wrong on a scaled display. A
            // window restored onto an unplugged monitor is unreachable.
            var corner = new PixelPoint((int)rect.Value.X, (int)rect.Value.Y);
            if (IsOnAScreen(window, corner))
                window.Position = corner;
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

            // Before the macOS guard: geometry is not platform specific.
            Restore(window, state);

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
