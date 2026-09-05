using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using EQTool.Avalonia.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EQTool.Avalonia.Services
{
    // Owns every window the app can open.
    //
    // This is a single shared registry rather than one per opener. Each secondary
    // window subscribes to log events and owns a timer, so a second instance
    // double-counts every update. The tray menu and the header buttons both route
    // here so they cannot each hold their own idea of what is open.
    public static class WindowManager
    {
        private static readonly Dictionary<Type, Window> OpenWindows = new Dictionary<Type, Window>();

        public static void ShowTimers() => Show(() => new MainWindow());

        public static void ShowMap() => Show(() => new MapWindow());

        public static void ShowDps() => Show(() => new DpsWindow());

        public static void ShowSettings() => Show(() => new SettingsWindow());

        public static void ShowOverlay() => Show(() => new EventOverlayWindow());

        public static void ShowConsole() => Show(() => new ConsoleWindow());

        public static void ShowMobInfo() => Show(() => new MobInfoWindow());

        public static void Show<TWindow>(Func<TWindow> create) where TWindow : Window
        {
            if (OpenWindows.TryGetValue(typeof(TWindow), out var existing))
            {
                existing.Show();
                existing.Activate();
                return;
            }

            var window = create();
            OpenWindows[typeof(TWindow)] = window;
            window.Closed += (_, _) => OpenWindows.Remove(typeof(TWindow));
            window.Show();
            window.Activate();
        }

        public static bool TryGet<TWindow>(out TWindow window) where TWindow : Window
        {
            if (OpenWindows.TryGetValue(typeof(TWindow), out var existing) && existing is TWindow typed)
            {
                window = typed;
                return true;
            }

            window = null;
            return false;
        }

        // The overlay reads this setting when it opens, so without this a change
        // does nothing until it is reopened. That matters most when turning
        // click-through back off, which is the only way to regain the drag handle
        // and move the overlay.
        public static void ApplyOverlayClickThrough(bool clickThrough)
        {
            if (TryGet<EventOverlayWindow>(out var overlay))
                WindowPreferences.SetClickThrough(overlay, clickThrough);
        }

        // Same reason. Always-on-top and opacity are read when a window opens, so
        // the settings row does nothing to the window you are looking at without
        // this. Opacity is the obvious one: the slider moves and the window does
        // not.
        public static void ApplyPreferencesTo<TWindow>(EQTool.Models.WindowState state, bool asOverlay = false)
            where TWindow : Window
        {
            if (TryGet<TWindow>(out var window))
                WindowPreferences.ApplyNow(window, state, asOverlay);
        }

        // Registers a window the app created itself, so the tray does not open a
        // second copy of something already on screen.
        public static void Adopt(Window window)
        {
            if (window == null)
                return;

            var key = window.GetType();
            OpenWindows[key] = window;
            window.Closed += (_, _) => OpenWindows.Remove(key);
        }

        public static void OpenUrl(string url)
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception failure)
            {
                Console.Error.WriteLine("[pigparse] could not open " + url + ": " + failure.Message);
            }
        }

        public static void Quit()
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }
}
