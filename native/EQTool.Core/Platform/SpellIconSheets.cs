using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace EQTool.Core.Platform
{
    // Supplies the spell icon sheets as PNG bytes.
    //
    // Upstream's SpellIcons decodes the .tga sheets with TGASharpLib and hands
    // back a System.Drawing bitmap, which does not exist here. The geometry is
    // already handled: SpellExtensions.Map is linked and running, so each Spell
    // already carries the sheet number on SpellIcon.SpellIndex and a 40x40 Rect.
    // Only the pixels are missing, so this returns the sheet and leaves cropping
    // to whichever UI toolkit is in front.
    public static class SpellIconSheets
    {
        // SpellExtensions.Map slices a 6x6 grid of these out of each sheet.
        public const int IconSize = 40;

        public const int LowestSheetIndex = 1;

        public const int HighestSheetIndex = 7;

        private const string ResourceNameFormat = "EQTool.Core.Spells.spells{0:00}.png";

        private static readonly ConcurrentDictionary<int, byte[]> CachedSheets =
            new ConcurrentDictionary<int, byte[]>();

        // Null when the index is outside the sheets that ship, so a caller can
        // fall back to no icon rather than being handed empty pixels.
        public static byte[] GetSheetPng(int sheetIndex)
        {
            if (sheetIndex < LowestSheetIndex || sheetIndex > HighestSheetIndex)
                return null;

            return CachedSheets.GetOrAdd(sheetIndex, ReadEmbeddedSheet);
        }

        private static byte[] ReadEmbeddedSheet(int sheetIndex)
        {
            var resourceName = string.Format(ResourceNameFormat, sheetIndex);
            var assembly = typeof(SpellIconSheets).Assembly;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;

                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    return buffer.ToArray();
                }
            }
        }
    }
}
