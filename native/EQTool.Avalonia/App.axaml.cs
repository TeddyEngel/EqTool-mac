using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.Views;
using System;

namespace EQTool.Avalonia
{
    public partial class App : Application
    {
        private const string GithubUrl = "https://github.com/smasherprog/EqTool";
        private const string DiscordUrl = "https://discord.gg/pp3sM4wFEE";

        private TrayIcon trayIcon;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Removing this exits the process when the last window closes,
                // which would stop the log parser.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;

                // Without this the tray opens a second MainWindow, duplicating its
                // log subscriptions.
                WindowManager.Adopt(mainWindow);

                trayIcon = CreateTrayIcon();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private TrayIcon CreateTrayIcon()
        {
            var menu = new NativeMenu();
            menu.Add(MenuItem("Timers", WindowManager.ShowTimers));
            menu.Add(MenuItem("Overlay", WindowManager.ShowOverlay));
            menu.Add(MenuItem("Map", WindowManager.ShowMap));
            menu.Add(MenuItem("DPS Meter", WindowManager.ShowDps));
            menu.Add(MenuItem("Mob Info", WindowManager.ShowMobInfo));
            menu.Add(MenuItem("Settings", WindowManager.ShowSettings));
            menu.Add(MenuItem("Console", WindowManager.ShowConsole));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(MenuItem("Discord", () => WindowManager.OpenUrl(DiscordUrl)));
            menu.Add(MenuItem("GitHub", () => WindowManager.OpenUrl(GithubUrl)));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(MenuItem("Quit PigParse", WindowManager.Quit));

            var icon = new TrayIcon
            {
                Icon = LoadTrayIcon(),
                ToolTipText = "PigParse",
                Menu = menu,
                IsVisible = true
            };

            icon.Clicked += (_, _) => WindowManager.ShowTimers();

            return icon;
        }

        private static NativeMenuItem MenuItem(string header, Action action)
        {
            var item = new NativeMenuItem(header);
            item.Click += (_, _) => action();
            return item;
        }

        private static WindowIcon LoadTrayIcon()
        {
            var uri = new Uri("avares://EQTool.Avalonia/Assets/tray-icon.png");
            using (var stream = AssetLoader.Open(uri))
            {
                return new WindowIcon(stream);
            }
        }
    }
}
