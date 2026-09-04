using EQTool.Core.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class SpellIconSheetsTests
    {
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };

        [TestMethod]
        public void GetSheetPng_EverySheetUpstreamCanReference_ReturnsPngBytes()
        {
            // Arrange
            // SpellExtensions.Map only ever asks for sheets 1 through 7.
            var sheetIndexes = Enumerable.Range(
                SpellIconSheets.LowestSheetIndex,
                SpellIconSheets.HighestSheetIndex - SpellIconSheets.LowestSheetIndex + 1);

            foreach (var sheetIndex in sheetIndexes)
            {
                // Act
                var sheet = SpellIconSheets.GetSheetPng(sheetIndex);

                // Assert
                Assert.IsNotNull(sheet, $"Sheet {sheetIndex} is missing.");
                CollectionAssert.AreEqual(
                    PngMagic,
                    sheet.Take(PngMagic.Length).ToArray(),
                    $"Sheet {sheetIndex} is not a PNG.");
            }
        }

        [TestMethod]
        public void GetSheetPng_IndexBelowRange_ReturnsNull()
        {
            // Act
            var sheet = SpellIconSheets.GetSheetPng(0);

            // Assert
            Assert.IsNull(sheet);
        }

        [TestMethod]
        public void GetSheetPng_IndexAboveRange_ReturnsNull()
        {
            // Act
            var sheet = SpellIconSheets.GetSheetPng(SpellIconSheets.HighestSheetIndex + 1);

            // Assert
            Assert.IsNull(sheet);
        }

        [TestMethod]
        public void GetSheetPng_CalledTwice_ReturnsTheSameCachedInstance()
        {
            // Act
            var first = SpellIconSheets.GetSheetPng(SpellIconSheets.LowestSheetIndex);
            var second = SpellIconSheets.GetSheetPng(SpellIconSheets.LowestSheetIndex);

            // Assert
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void IconSize_MatchesTheRectUpstreamComputes()
        {
            // Arrange
            // SpellExtensions.Map hardcodes new Int32Rect(x, y, 40, 40). If that
            // ever changes, cropping silently produces the wrong icon rather
            // than failing, so pin it here.
            const int rectSizeUsedByUpstream = 40;

            // Assert
            Assert.AreEqual(rectSizeUsedByUpstream, SpellIconSheets.IconSize);
        }
    }
}
