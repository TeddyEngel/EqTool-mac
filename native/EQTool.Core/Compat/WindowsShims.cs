// WPF/Windows-only type shims.
//
// Upstream source files that we link into this net9.0 assembly reference types
// from `System.Windows`, `System.Windows.Media`, `System.Windows.Media.Imaging`,
// and `System.Windows.Data` that only exist under WPF on Windows. We are not
// allowed to edit the upstream files, so we declare *just enough* of those
// types here to make the compiler happy. Anything that runs on macOS only
// touches trivial getters/setters and object-initializer syntax on these
// types; parsers and handlers never actually paint pixels, allocate GPU
// resources, refresh WPF collection views, or read bitmap data at test time.
//
// Declaring types inside System.* namespaces is legal C# and is the same
// technique the .NET runtime team uses for polyfill/shim packages.

using System;
using System.Collections;

namespace System.Windows
{
    public enum Visibility
    {
        Visible = 0,
        Hidden = 1,
        Collapsed = 2,
    }

    public enum WindowState
    {
        Normal = 0,
        Minimized = 1,
        Maximized = 2,
    }

    public struct Rect
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public Rect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public struct Int32Rect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Int32Rect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public struct Point
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}

namespace System.Windows.Media
{
    // Represents any brush the WPF code paints with. Upstream code only stores
    // and passes references; it never queries pixels, so a marker base class is
    // enough. `SolidColorBrush` is used with an object initializer in one
    // place (`PetViewModel`), so it must be constructible with a parameterless
    // ctor and expose a settable `Color`.
    public class Brush
    {
        public bool CanFreeze => true;

        public void Freeze()
        {
        }
    }

    public class BrushConverter
    {
        public object ConvertFromString(string value)
        {
            return new SolidColorBrush(NamedColors.Parse(value));
        }

        public object ConvertFrom(object value)
        {
            return ConvertFromString(value as string);
        }
    }

    internal static class NamedColors
    {
        private static readonly System.Collections.Generic.Dictionary<string, Color> Map
            = new System.Collections.Generic.Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "White",           Color.FromRgb(255, 255, 255) },
            { "Black",           Color.FromRgb(  0,   0,   0) },
            { "Red",             Color.FromRgb(255,   0,   0) },
            { "Orange",          Color.FromRgb(255, 165,   0) },
            { "Gold",            Color.FromRgb(255, 215,   0) },
            { "Yellow",          Color.FromRgb(255, 255,   0) },
            { "GreenYellow",     Color.FromRgb(173, 255,  47) },
            { "LimeGreen",       Color.FromRgb( 50, 205,  50) },
            { "Green",           Color.FromRgb(  0, 128,   0) },
            { "DarkGreen",       Color.FromRgb(  0, 100,   0) },
            { "SpringGreen",     Color.FromRgb(  0, 255, 127) },
            { "SeaGreen",        Color.FromRgb( 46, 139,  87) },
            { "MediumSeaGreen",  Color.FromRgb( 60, 179, 113) },
            { "DarkSeaGreen",    Color.FromRgb(143, 188, 143) },
            { "LightSeaGreen",   Color.FromRgb( 32, 178, 170) },
            { "LightGreen",      Color.FromRgb(144, 238, 144) },
            { "ForestGreen",     Color.FromRgb( 34, 139,  34) },
            { "MediumAquamarine",Color.FromRgb(102, 205, 170) },
            { "Aquamarine",      Color.FromRgb(127, 255, 212) },
            { "Cyan",            Color.FromRgb(  0, 255, 255) },
            { "DeepSkyBlue",     Color.FromRgb(  0, 191, 255) },
            { "LightSkyBlue",    Color.FromRgb(135, 206, 250) },
            { "SkyBlue",         Color.FromRgb(135, 206, 235) },
            { "LightBlue",       Color.FromRgb(173, 216, 230) },
            { "CornflowerBlue",  Color.FromRgb(100, 149, 237) },
            { "SteelBlue",       Color.FromRgb( 70, 130, 180) },
            { "CadetBlue",       Color.FromRgb( 95, 158, 160) },
            { "MediumPurple",    Color.FromRgb(147, 112, 219) },
            { "Magenta",         Color.FromRgb(255,   0, 255) },
            { "HotPink",         Color.FromRgb(255, 105, 180) },
            { "DeepPink",        Color.FromRgb(255,  20, 147) },
            { "LightPink",       Color.FromRgb(255, 182, 193) },
            { "Pink",            Color.FromRgb(255, 192, 203) },
            { "Salmon",          Color.FromRgb(250, 128, 114) },
            { "LightSalmon",     Color.FromRgb(255, 160, 122) },
            { "DarkSalmon",      Color.FromRgb(233, 150, 122) },
            { "OrangeRed",       Color.FromRgb(255,  69,   0) },
            { "DarkRed",         Color.FromRgb(139,   0,   0) },
            { "DarkGoldenrod",   Color.FromRgb(184, 134,  11) },
            { "Chocolate",       Color.FromRgb(210, 105,  30) },
            { "Gray",            Color.FromRgb(128, 128, 128) },
            { "LightSlateGray",  Color.FromRgb(119, 136, 153) },
            { "WhiteSmoke",      Color.FromRgb(245, 245, 245) },
            { "Transparent",     Color.FromArgb(  0, 255, 255, 255) },
        };

        public static Color Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return default;
            }
            var trimmed = value.Trim();
            if (Map.TryGetValue(trimmed, out var color))
            {
                return color;
            }
            if (trimmed[0] == '#')
            {
                return ParseHex(trimmed.Substring(1));
            }
            return default;
        }

        private static Color ParseHex(string hex)
        {
            if (hex.Length == 6)
            {
                return Color.FromRgb(HexByte(hex, 0), HexByte(hex, 2), HexByte(hex, 4));
            }
            if (hex.Length == 8)
            {
                return Color.FromArgb(HexByte(hex, 0), HexByte(hex, 2), HexByte(hex, 4), HexByte(hex, 6));
            }
            return default;
        }

        private static byte HexByte(string hex, int index)
        {
            return Convert.ToByte(hex.Substring(index, 2), 16);
        }
    }

    public class SolidColorBrush : Brush
    {
        public Color Color { get; set; }

        public SolidColorBrush() { }

        public SolidColorBrush(Color color)
        {
            Color = color;
        }
    }

    public struct Color
    {
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public static Color FromArgb(byte a, byte r, byte g, byte b)
        {
            return new Color { A = a, R = r, G = g, B = b };
        }

        public static Color FromRgb(byte r, byte g, byte b)
        {
            return new Color { A = 255, R = r, G = g, B = b };
        }
    }

    // Named brushes used across handlers, viewmodels and DebugOutput.
    // Types declared as `SolidColorBrush` (e.g. `BaseTriggerViewModel.ProgressBarColor`)
    // must be assignable from any brush here, so every entry is a
    // `SolidColorBrush` rather than a bare `Brush`.
    public static class Brushes
    {
        private static SolidColorBrush Make(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static SolidColorBrush MakeArgb(byte a, byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }

        public static SolidColorBrush Aquamarine { get; } = Make(127, 255, 212);
        public static SolidColorBrush Black { get; } = Make(0, 0, 0);
        public static SolidColorBrush CadetBlue { get; } = Make(95, 158, 160);
        public static SolidColorBrush Chocolate { get; } = Make(210, 105, 30);
        public static SolidColorBrush Cyan { get; } = Make(0, 255, 255);
        public static SolidColorBrush DarkGoldenrod { get; } = Make(184, 134, 11);
        public static SolidColorBrush DarkGreen { get; } = Make(0, 100, 0);
        public static SolidColorBrush DarkRed { get; } = Make(139, 0, 0);
        public static SolidColorBrush DarkSalmon { get; } = Make(233, 150, 122);
        public static SolidColorBrush DarkSeaGreen { get; } = Make(143, 188, 143);
        public static SolidColorBrush DeepPink { get; } = Make(255, 20, 147);
        public static SolidColorBrush ForestGreen { get; } = Make(34, 139, 34);
        public static SolidColorBrush Gold { get; } = Make(255, 215, 0);
        public static SolidColorBrush Gray { get; } = Make(128, 128, 128);
        public static SolidColorBrush Green { get; } = Make(0, 128, 0);
        public static SolidColorBrush HotPink { get; } = Make(255, 105, 180);
        public static SolidColorBrush LightBlue { get; } = Make(173, 216, 230);
        public static SolidColorBrush LightGreen { get; } = Make(144, 238, 144);
        public static SolidColorBrush LightPink { get; } = Make(255, 182, 193);
        public static SolidColorBrush LightSalmon { get; } = Make(255, 160, 122);
        public static SolidColorBrush LightSeaGreen { get; } = Make(32, 178, 170);
        public static SolidColorBrush LightSkyBlue { get; } = Make(135, 206, 250);
        public static SolidColorBrush LightSlateGray { get; } = Make(119, 136, 153);
        public static SolidColorBrush MediumAquamarine { get; } = Make(102, 205, 170);
        public static SolidColorBrush MediumPurple { get; } = Make(147, 112, 219);
        public static SolidColorBrush Orange { get; } = Make(255, 165, 0);
        public static SolidColorBrush OrangeRed { get; } = Make(255, 69, 0);
        public static SolidColorBrush Red { get; } = Make(255, 0, 0);
        public static SolidColorBrush SkyBlue { get; } = Make(135, 206, 235);
        public static SolidColorBrush SteelBlue { get; } = Make(70, 130, 180);
        public static SolidColorBrush Transparent { get; } = MakeArgb(0, 255, 255, 255);
        public static SolidColorBrush White { get; } = Make(255, 255, 255);
        public static SolidColorBrush WhiteSmoke { get; } = Make(245, 245, 245);
        public static SolidColorBrush Yellow { get; } = Make(255, 255, 0);
    }

    public class ImageSource
    {
        public bool CanFreeze => true;

        public void Freeze()
        {
        }
    }

    // Named `Color` values used by SpellWindowViewModel's gradient brushes.
    // The upstream code only pushes these into `GradientStop` values and never
    // reads them back, so the shim exposes the same names without touching a
    // real pixel value.
    public static class Colors
    {
        public static Color CadetBlue { get; } = Color.FromRgb(95, 158, 160);
        public static Color Gray { get; } = Color.FromRgb(128, 128, 128);
        public static Color OrangeRed { get; } = Color.FromRgb(255, 69, 0);
    }

    // Gradient brush support is used only to paint the SpellWindowViewModel's
    // header. In the headless core the values are stored on the viewmodel but
    // never rendered.
    public class GradientBrush : Brush
    {
        public GradientStopCollection GradientStops { get; set; }
    }

    public class LinearGradientBrush : GradientBrush
    {
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
    }

    public class GradientStop
    {
        public Color Color { get; set; }
        public double Offset { get; set; }

        public GradientStop() { }
        public GradientStop(Color color, double offset)
        {
            Color = color;
            Offset = offset;
        }
    }

    public class GradientStopCollection : System.Collections.ObjectModel.Collection<GradientStop>
    {
    }

    // MediaPlayer is exercised only by `AudioService.Play(...)`, which the
    // handlers may build in DI but which no test triggers audibly. The stub
    // has to expose the same members `AudioService` sets (Volume/Stop/Open/Play)
    // and be constructible.
    public class MediaPlayer
    {
        public double Volume { get; set; }

        public void Stop()
        {
        }

        public void Open(Uri source)
        {
        }

        public void Play()
        {
        }
    }
}

namespace System.Windows.Media.Imaging
{
    public class BitmapSource : System.Windows.Media.ImageSource
    {
    }

    public enum BitmapCacheOption
    {
        Default = 0,
        OnDemand = 1,
        OnLoad = 2,
        None = 3,
    }

    public class BitmapImage : BitmapSource
    {
        public System.IO.Stream StreamSource { get; set; }
        public BitmapCacheOption CacheOption { get; set; }
        public void BeginInit() { }
        public void EndInit() { }
    }

    public class CroppedBitmap : BitmapSource
    {
        public CroppedBitmap() { }
        public CroppedBitmap(BitmapSource source, System.Windows.Int32Rect sourceRect) { }
    }
}

namespace System.Windows.Data
{
    // WPF's `CollectionViewSource.GetDefaultView(collection)` returns an
    // `ICollectionView` (typically `ListCollectionView`). Handlers and
    // viewmodels configure grouping, sorting, and call `Refresh()` on it.
    // None of that behavior matters in a headless test run — grouping and
    // sorting are UI concerns — so the shim is a no-op that satisfies the
    // compiler.
    public static class CollectionViewSource
    {
        public static ListCollectionView GetDefaultView(object source)
        {
            return new ListCollectionView(source as IEnumerable);
        }
    }

    public class ListCollectionView
    {
        public ListCollectionView() { }
        public ListCollectionView(IEnumerable source) { }

        public System.Collections.ObjectModel.Collection<GroupDescription> GroupDescriptions { get; }
            = new System.Collections.ObjectModel.Collection<GroupDescription>();

        public System.Collections.ObjectModel.Collection<SortDescription> SortDescriptions { get; }
            = new System.Collections.ObjectModel.Collection<SortDescription>();

        public System.Collections.ObjectModel.Collection<string> LiveGroupingProperties { get; }
            = new System.Collections.ObjectModel.Collection<string>();

        public System.Collections.ObjectModel.Collection<string> LiveSortingProperties { get; }
            = new System.Collections.ObjectModel.Collection<string>();

        public bool IsLiveGrouping { get; set; }
        public bool IsLiveSorting { get; set; }

        public void Refresh() { }
    }

    public abstract class GroupDescription
    {
    }

    public class PropertyGroupDescription : GroupDescription
    {
        public string PropertyName { get; set; }

        public PropertyGroupDescription() { }
        public PropertyGroupDescription(string propertyName)
        {
            PropertyName = propertyName;
        }
    }

    public struct SortDescription
    {
        public string PropertyName { get; set; }
        public System.ComponentModel.ListSortDirection Direction { get; set; }

        public SortDescription(string propertyName, System.ComponentModel.ListSortDirection direction)
        {
            PropertyName = propertyName;
            Direction = direction;
        }
    }

    public interface IValueConverter
    {
        object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);
        object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture);
    }

    public static class Binding
    {
        public static readonly object DoNothing = new object();
    }
}

namespace System.Windows.Controls
{
    public class ValidationResult
    {
        public bool IsValid { get; }
        public object ErrorContent { get; }

        public static ValidationResult ValidResult { get; } = new ValidationResult(true, null);

        public ValidationResult(bool isValid, object errorContent)
        {
            IsValid = isValid;
            ErrorContent = errorContent;
        }
    }

    public abstract class ValidationRule
    {
        public abstract ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo);
    }

    public class UserControl
    {
    }
}

namespace System.Windows.Documents
{
    internal class NamespaceMarker { }
}

namespace System.Windows.Threading
{
    // A host able to run a repeating callback on its UI thread. A UI shell
    // installs one so the shim below becomes a real timer; a headless run
    // leaves it null and the shim stays inert, exactly as it was.
    public interface IDispatcherTimerHost
    {
        IDisposable Schedule(TimeSpan interval, Action onTick);
    }

    // WPF's DispatcherTimer raises Tick on the UI thread.
    //
    // With no host installed this is a no-op: Start() flips a flag and nothing
    // ever ticks. That is deliberate for a headless test run, where a live
    // background timer would fire trigger output asynchronously in the middle
    // of assertions.
    //
    // It is *not* good enough at runtime. TriggerTimerManager keeps its list of
    // running timers pruned from Tick; if Tick never fires, an expired timer
    // stays in that list forever and the next match takes the "restart the
    // existing one" branch - which updates a viewmodel that was already removed
    // from the spell list, so the row never comes back. Installing a host fixes
    // that without changing anything for callers that do not.
    public class DispatcherTimer
    {
        public static IDispatcherTimerHost Host { get; set; }

        private IDisposable subscription;

        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; private set; }
        public event EventHandler Tick;

        public void Start()
        {
            IsEnabled = true;
            subscription?.Dispose();
            subscription = Host?.Schedule(Interval, RaiseTick);
        }

        public void Stop()
        {
            IsEnabled = false;
            subscription?.Dispose();
            subscription = null;
        }

        internal void RaiseTick()
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}
