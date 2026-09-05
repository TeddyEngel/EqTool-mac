using Avalonia;
using System;
using System.Collections.Generic;

namespace EQTool.Avalonia.Services
{
    // Recomputes the type tokens from the user's font size.
    //
    // The scale in Theme/DesignTokens.axaml is deliberate: the countdown is the
    // only thing allowed to be large, and the group label sits at the bottom.
    // Multiplying every token by the same factor keeps that ordering and the
    // gaps between the steps, so this is arithmetic rather than a second opinion
    // about the design.
    //
    // The base is 12 because that is what TypeBody and TypeRow are set to, and
    // it is also the default of the font size setting, so leaving the slider
    // alone reproduces the file exactly.
    public static class TypeScale
    {
        public const double BaseFontSize = 12.0;

        // The values as written in DesignTokens.axaml.
        private static readonly Dictionary<string, double> Defaults = new Dictionary<string, double>
        {
            ["TypeMicro"] = 9,
            ["TypeCaption"] = 11,
            ["TypeBody"] = 12,
            ["TypeRow"] = 12,
            ["TypeCountdown"] = 13,
            ["TypeHeading"] = 16,
            ["TypeTitle"] = 20,
        };

        public static IReadOnlyDictionary<string, double> DefaultTokens => Defaults;

        // Rounded to a half point. Text rendered at an arbitrary fraction is
        // blurrier than the same text at a rounded size, and the difference is
        // visible on the small end of the scale.
        public static double ScaleToken(double token, double fontSize)
        {
            var scaled = token * fontSize / BaseFontSize;
            return Math.Round(scaled * 2, MidpointRounding.AwayFromZero) / 2;
        }

        public static Dictionary<string, double> Compute(double fontSize)
        {
            var result = new Dictionary<string, double>(Defaults.Count);
            foreach (var pair in Defaults)
                result[pair.Key] = ScaleToken(pair.Value, fontSize);

            return result;
        }

        // Writes into the application's resources, which is where
        // DesignTokens.axaml is merged. Consumers have to ask for these with
        // DynamicResource; a StaticResource is resolved once when the view loads
        // and would not notice.
        public static void Apply(double fontSize)
        {
            var application = Application.Current;
            if (application == null)
                return;

            foreach (var pair in Compute(fontSize))
                application.Resources[pair.Key] = pair.Value;
        }
    }
}
