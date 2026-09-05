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
