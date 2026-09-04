using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EQTool.Core.Platform;

namespace EQTool.Avalonia.Services
{
    // Turns the sheet number and crop rectangle a trigger already carries into a
    // drawable bitmap.
    //
    // Upstream does this in markup: a `CroppedBitmap` over the `BitmapImage` that
    // `SpellIcons` decoded from the .tga sheets. Neither half of that exists on
    // macOS, so the pixels come from `SpellIconSheets` as PNG and the crop is done
    // here with a pixel copy.
    //
    // Both halves are cached because neither is cheap and the window asks for them
    // constantly: the list is rebuilt from a 100 ms tick with tens of visible rows,
    // and decoding a 256x256 PNG per row per tick would dominate the frame. A sheet
    // is decoded at most once, and each distinct crop is cut at most once.
    //
    // `LogParser` raises its updates from a timer thread, so rows - and therefore
    // these lookups - can arrive from off the UI thread. Both caches are concurrent
    // and the factories are held in `Lazy` so a sheet is never decoded twice.
    public class SpellIconService
    {
        // The sheets were baked at 1:1, so the crop rectangles upstream computes
        // are device pixels and the result should claim no scaling of its own.
        private static readonly Vector IconDpi = new Vector(96, 96);

        private readonly ConcurrentDictionary<int, Lazy<Bitmap>> sheets
            = new ConcurrentDictionary<int, Lazy<Bitmap>>();

        private readonly ConcurrentDictionary<IconKey, Lazy<Bitmap>> icons
            = new ConcurrentDictionary<IconKey, Lazy<Bitmap>>();

        // Null whenever the row should simply show no icon: an index outside the
        // sheets that ship, a rectangle of no size, or one that falls off the sheet.
        public Bitmap GetIcon(int sheetIndex, System.Windows.Int32Rect rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return null;

            if (sheetIndex < SpellIconSheets.LowestSheetIndex || sheetIndex > SpellIconSheets.HighestSheetIndex)
                return null;

            var key = new IconKey(sheetIndex, rect.X, rect.Y, rect.Width, rect.Height);
            return icons.GetOrAdd(key, CreateIconFactory).Value;
        }

        private Lazy<Bitmap> CreateIconFactory(IconKey key)
        {
            return new Lazy<Bitmap>(() => CutIcon(key), true);
        }

        private Bitmap CutIcon(IconKey key)
        {
            var sheet = GetSheet(key.SheetIndex);
            if (sheet == null)
                return null;

            var crop = new PixelRect(key.X, key.Y, key.Width, key.Height);
            var sheetBounds = new PixelRect(sheet.PixelSize);
            if (!sheetBounds.Contains(crop))
                return null;

            return Crop(sheet, crop);
        }

        private Bitmap GetSheet(int sheetIndex)
        {
            return sheets.GetOrAdd(sheetIndex, index => new Lazy<Bitmap>(() => DecodeSheet(index), true)).Value;
        }

        private static Bitmap DecodeSheet(int sheetIndex)
        {
            var png = SpellIconSheets.GetSheetPng(sheetIndex);
            if (png == null)
                return null;

            using (var stream = new MemoryStream(png))
            {
                return new Bitmap(stream);
            }
        }

        private static Bitmap Crop(Bitmap sheet, PixelRect crop)
        {
            var format = sheet.Format ?? PixelFormat.Bgra8888;
            var alphaFormat = sheet.AlphaFormat ?? AlphaFormat.Premul;

            var bytesPerPixel = (format.BitsPerPixel + 7) / 8;
            var stride = crop.Width * bytesPerPixel;
            var pixels = new byte[stride * crop.Height];

            var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                var address = pinned.AddrOfPinnedObject();
                sheet.CopyPixels(crop, address, pixels.Length, stride);
                return new Bitmap(format, alphaFormat, address, crop.Size, IconDpi, stride);
            }
            finally
            {
                pinned.Free();
            }
        }

        // Two rows showing the same spell must resolve to the same cached crop, so
        // the key is the sheet plus the whole rectangle rather than the rect shim,
        // which is a mutable struct with no value equality of its own.
        private readonly struct IconKey : IEquatable<IconKey>
        {
            public IconKey(int sheetIndex, int x, int y, int width, int height)
            {
                SheetIndex = sheetIndex;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public int SheetIndex { get; }

            public int X { get; }

            public int Y { get; }

            public int Width { get; }

            public int Height { get; }

            public bool Equals(IconKey other)
            {
                return SheetIndex == other.SheetIndex
                    && X == other.X
                    && Y == other.Y
                    && Width == other.Width
                    && Height == other.Height;
            }

            public override bool Equals(object obj)
            {
                return obj is IconKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (SheetIndex, X, Y, Width, Height).GetHashCode();
            }
        }
    }
}
