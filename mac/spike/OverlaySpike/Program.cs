using System;
using Avalonia;

namespace OverlaySpike;

internal static class Program
{
    // Parsed CLI settings, consumed by App/MainWindow after Avalonia starts.
    public static int WindowLevel { get; private set; } = 3; // NSFloatingWindowLevel default
    public static bool ClickThrough { get; private set; } = false;
    public static bool JoinAllSpaces { get; private set; } = true;

    [STAThread]
    public static int Main(string[] args)
    {
        ParseArgs(args);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--level":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int level))
                    {
                        WindowLevel = level;
                        i++;
                    }
                    break;
                case "--clickthrough":
                    ClickThrough = true;
                    break;
                case "--no-join-spaces":
                    JoinAllSpaces = false;
                    break;
                case "--help":
                case "-h":
                    Console.WriteLine("OverlaySpike --level <int> [--clickthrough] [--no-join-spaces]");
                    Console.WriteLine("  --level N          NSWindow level (0=Normal, 3=Floating, 25=Status, 27=Status+2, 1000=ScreenSaver)");
                    Console.WriteLine("  --clickthrough     Enable setIgnoresMouseEvents:YES");
                    Console.WriteLine("  --no-join-spaces   Do not set NSWindowCollectionBehaviorCanJoinAllSpaces");
                    break;
            }
        }
    }
}
