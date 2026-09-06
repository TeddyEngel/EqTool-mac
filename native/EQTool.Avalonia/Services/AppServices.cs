using Autofac;
using EQTool.Services;
using System;

namespace EQTool.Avalonia.Services
{
    // The single composition root for the client.
    //
    // This exists because the container must be built exactly once. LogParser
    // owns a 100 ms timer and FileReader carries the tail offset for the log
    // file, so a second container would mean a second parser reading the same
    // file from a different position, a duplicate event stream, and every
    // handler firing twice. With one window that was invisible; with several it
    // would be a real fault.
    public sealed class AppServices : IDisposable
    {
        private static AppServices current;

        private AppServices(SettingsBootstrapResult bootstrap, IContainer container)
        {
            Bootstrap = bootstrap;
            Container = container;
        }

        public SettingsBootstrapResult Bootstrap { get; }

        public IContainer Container { get; }

        public static AppServices Current => current
            ?? throw new InvalidOperationException(
                "AppServices.Initialize must run before anything resolves a service.");

        public static AppServices Initialize()
        {
            if (current != null)
                return current;

            AvaloniaDispatcherTimerHost.Install();

            var bootstrap = SettingsBootstrap.Load();
            var container = NativeContainer.Build(bootstrap);

            current = new AppServices(bootstrap, container);

            // Resolving TriggerTimerManager is what subscribes trigger timers to
            // the log stream: TriggerHandler takes it by constructor injection,
            // and handlers are only built when LogParser first asks for them.
            _ = container.Resolve<TriggerTimerManager>();

            // Constructing LogParser starts the poll, so it happens once, here,
            // rather than whenever a window happens to open.
            _ = container.Resolve<LogParser>();

            // Same again: the hourly check runs from this constructor. It is safe
            // to build unconditionally because the work is gated on
            // LogArchiveEnabled, which is off unless the user turns it on.
            _ = container.Resolve<LogArchiveService>();

            // Lets a window write its position back when it closes. Nothing else
            // installs this, so tests keep their settings in memory.
            WindowPreferences.Persist = () => bootstrap.Loader.Save(bootstrap.Settings);

            // Core owns the eqgame name match but has no way to reach AppKit. Nothing
            // else installs this, so tests and headless runs fall back to reporting
            // the game as not focused.
            EQTool.Core.Platform.EqGameFocus.FrontmostProcessId = () =>
                OperatingSystem.IsMacOS() ? MacOSWindowInterop.TryGetFrontmostProcessId() : null;

            return current;
        }

        public T Resolve<T>() => Container.Resolve<T>();

        public void Dispose()
        {
            Container?.Dispose();
            current = null;
        }
    }
}
