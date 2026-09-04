// WPF/Windows-only type shims.
//
// Upstream source files that we link into this net9.0 assembly reference types
// from `System.Windows`, `System.Windows.Media`, and `System.Windows.Media.Imaging`
// that only exist under WPF on Windows. We are not allowed to edit the upstream
// files, so we declare *just enough* of those types here to make the compiler
// happy. Anything that runs on macOS only touches trivial getters/setters and
// object-initializer syntax on these types; parsers never actually paint
// pixels, allocate GPU resources, or read bitmap data at test time.
//
// Declaring types inside System.* namespaces is legal C# and is the same
// technique the .NET runtime team uses for polyfill/shim packages.

using System;

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
}

namespace System.Windows.Media
{
    // Represents any brush the WPF code paints with. Upstream code only stores
    // and passes references; it never queries pixels, so a marker base class is
    // enough. SolidColorBrush is used with an object initializer in one place
    // (PetViewModel), so it must be constructible with a parameterless ctor and
    // expose a settable Color.
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
            return new SolidColorBrush();
        }

        public object ConvertFrom(object value)
        {
            return new SolidColorBrush();
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

    // Named brushes used by DebugOutput and PetViewModel. Each is a distinct
    // Brush instance so identity comparisons keep the same semantics they had
    // on Windows.
    public static class Brushes
    {
        public static Brush White { get; } = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        public static Brush Black { get; } = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        public static Brush Red { get; } = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        public static Brush Green { get; } = new SolidColorBrush(Color.FromRgb(0, 128, 0));
        public static Brush Orange { get; } = new SolidColorBrush(Color.FromRgb(255, 165, 0));
        public static Brush DarkSalmon { get; } = new SolidColorBrush(Color.FromRgb(233, 150, 122));
        public static Brush Cyan { get; } = new SolidColorBrush(Color.FromRgb(0, 255, 255));
        public static Brush LightSlateGray { get; } = new SolidColorBrush(Color.FromRgb(119, 136, 153));
        public static Brush Transparent { get; } = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
        public static Brush DarkGreen { get; } = new SolidColorBrush(Color.FromRgb(0, 100, 0));
        public static Brush LightGreen { get; } = new SolidColorBrush(Color.FromRgb(144, 238, 144));
    }

    public class ImageSource
    {
        public bool CanFreeze => true;

        public void Freeze()
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
