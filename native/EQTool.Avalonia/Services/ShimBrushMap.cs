using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Collections.Generic;
using WpfBrush = System.Windows.Media.Brush;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace EQTool.Avalonia.Services
{
    // Turns the WPF brushes upstream hands out into ones Avalonia can paint with.
    //
    // The shim's Brush in EQTool.Core/Compat/WindowsShims.cs is a marker class
    // with no colour on it at all; the colour lives on the SolidColorBrush
    // subclass. Console lines, trigger banner text and timer bar colours all
    // arrive as that subclass carrying real ARGB bytes. Anything else has no
    // colour to read and falls back to the theme rather than to a colour
    // invented here.
    public static class ShimBrushMap
    {
        private const string FallbackTokenKey = "BrushTextPrimary";

        // Upstream's brushes are singletons drawn from a small table, so caching
        // by instance keeps this to a handful of entries.
        private static readonly Dictionary<WpfBrush, IBrush> Resolved
            = new Dictionary<WpfBrush, IBrush>();

        public static IBrush Resolve(WpfBrush brush)
        {
            return Resolve(brush, FallbackTokenKey);
        }

        public static IBrush Resolve(WpfBrush brush, string fallbackTokenKey)
        {
            if (!(brush is WpfSolidColorBrush solid))
                return FromTheme(fallbackTokenKey);

            if (Resolved.TryGetValue(brush, out var existing))
                return existing;

            var colour = solid.Color;
            var converted = new ImmutableSolidColorBrush(
                Color.FromArgb(colour.A, colour.R, colour.G, colour.B));

            Resolved[brush] = converted;
            return converted;
        }

        public static IBrush FromTheme(string tokenKey)
        {
            if (Application.Current != null
                && Application.Current.TryFindResource(tokenKey, out var token)
                && token is IBrush brush)
            {
                return brush;
            }

            return null;
        }
    }
}
