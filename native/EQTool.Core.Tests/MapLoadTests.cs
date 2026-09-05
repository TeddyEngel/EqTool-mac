using EQTool.Models;
using EQTool.Services;
using EQTool.Services.Map;
using EQTool.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class MapLoadTests
    {
        private MapLoad CreateMapLoad()
        {
            var activePlayer = new ActivePlayer(new EQToolSettings(), new LogEvents());
            return new MapLoad(new LoggingService(), activePlayer);
        }

        [TestMethod]
        public void Load_KnownZone_ReturnsGeometryFromTheEmbeddedResource()
        {
            // Arrange
            var mapLoad = CreateMapLoad();

            // Act
            var parsed = mapLoad.Load("airplane");

            // Assert
            Assert.IsNotNull(parsed);
            Assert.IsTrue(parsed.Lines.Any(), "Expected line geometry for airplane.");
        }

        [TestMethod]
        public void Load_KnownZone_ProducesLinesWithDistinctPoints()
        {
            // Arrange
            var mapLoad = CreateMapLoad();

            // Act
            var parsed = mapLoad.Load("airplane");
            var line = parsed.Lines.First();

            // Assert
            Assert.IsNotNull(line.Points);
            Assert.IsTrue(line.Points.Length >= 2, "A map line needs at least two points.");
            Assert.AreNotEqual(line.Points[0], line.Points[1]);
        }

        [TestMethod]
        public void Load_KnownZone_ComputesABoundingBoxWithRealExtent()
        {
            // Arrange
            var mapLoad = CreateMapLoad();

            // Act
            var parsed = mapLoad.Load("airplane");

            // Assert
            Assert.IsTrue(parsed.AABB.Max.X > parsed.AABB.Min.X, "Bounding box has no width.");
            Assert.IsTrue(parsed.AABB.Max.Y > parsed.AABB.Min.Y, "Bounding box has no height.");
        }

        [TestMethod]
        public void Load_UnknownZone_ReturnsEmptyGeometryRatherThanThrowing()
        {
            // Arrange
            var mapLoad = CreateMapLoad();

            // Act
            var parsed = mapLoad.Load("thiszonedoesnotexist");

            // Assert
            Assert.IsNotNull(parsed);
            Assert.IsFalse(parsed.Lines.Any());
        }

        [TestMethod]
        public void Load_NullZone_FallsBackToWestFreeport()
        {
            // Arrange
            // MapLoad.Load substitutes "freportw" when the zone is blank.
            var mapLoad = CreateMapLoad();

            // Act
            var parsed = mapLoad.Load(null);

            // Assert
            Assert.IsTrue(parsed.Lines.Any(), "Expected the freportw fallback to load.");
        }

        [TestMethod]
        public void Load_ZoneWithLabels_ParsesLabelText()
        {
            // Arrange
            var mapLoad = CreateMapLoad();

            // Act
            var parsed = mapLoad.Load("airplane");

            // Assert
            Assert.IsTrue(
                parsed.Labels.Any(a => !string.IsNullOrWhiteSpace(a.label)),
                "Expected at least one non-empty map label.");
        }
    }
}
