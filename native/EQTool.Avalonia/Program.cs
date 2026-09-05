using System;
using Avalonia;

namespace EQTool.Avalonia
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            // First, before anything in the process builds a Regex. See RegexSafety.
            EQTool.Core.Platform.RegexSafety.Install();

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
