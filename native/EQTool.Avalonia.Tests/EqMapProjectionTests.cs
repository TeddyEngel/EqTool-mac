using EQTool.Avalonia.Controls;
using EQTool.Models;
using EQTool.Services.Map;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace EQTool.Avalonia.Tests
{
    // These pin the EverQuest axis convention.
    //
    // Getting it wrong does not look wrong. The map still renders as a coherent
    // map, just mirrored and rotated, and nothing about it reads as a defect. A
    // future simplification of ToScreenSpace to `new Point(x, y)` would pass a
    // visual glance and fail every test here.
    [TestClass]
    public class EqMapProjectionTests
    {
        [TestMethod]
        public void ToScreenSpace_MapsEqYOntoTheScreenXAxis()
        {
            // Act
            var projected = EqMapProjection.ToScreenSpace(eqX: 10, eqY: 400);

            // Assert
            // Screen X comes from EQ Y, not EQ X. Dropping the swap breaks this.
            Assert.AreEqual(-400, projected.X);
        }

        [TestMethod]
        public void ToScreenSpace_MapsEqXOntoTheScreenYAxis()
        {
            // Act
            var projected = EqMapProjection.ToScreenSpace(eqX: 10, eqY: 400);

            // Assert
            Assert.AreEqual(-10, projected.Y);
        }

        [TestMethod]
        public void ToScreenSpace_NegatesBothAxes()
        {
            // Act
            var projected = EqMapProjection.ToScreenSpace(eqX: 100, eqY: 200);

            // Assert
            // Dropping either negation mirrors the map along that axis.
            Assert.IsTrue(projected.X < 0, "Screen X should be negated, got " + projected.X);
            Assert.IsTrue(projected.Y < 0, "Screen Y should be negated, got " + projected.Y);
        }

        [TestMethod]
        public void ToScreenSpace_NegativeEqCoordinates_BecomePositiveScreenCoordinates()
        {
            // Arrange
            // EQ zones routinely run negative, so this is the common case rather
            // than an edge case.

            // Act
            var projected = EqMapProjection.ToScreenSpace(eqX: -50, eqY: -75);

            // Assert
            Assert.AreEqual(75, projected.X);
            Assert.AreEqual(50, projected.Y);
        }

        [TestMethod]
        public void ToScreenSpace_Origin_StaysAtTheOrigin()
        {
            // Act
            var projected = EqMapProjection.ToScreenSpace(0, 0);

            // Assert
            Assert.AreEqual(0, projected.X);
            Assert.AreEqual(0, projected.Y);
        }

        [TestMethod]
        public void ProjectPlayer_AppliesTheOffsetMapLoadSubtracted()
        {
            // Arrange
            // MapLoad shifts geometry to the origin and records what it removed on
            // ParsedData.Offset. A raw log position needs the same shift or the
            // marker sits somewhere else entirely.
            var location = new EqMapProjection.Point3DLike(120, 260);
            var offset = new EqMapProjection.Point3DLike(20, 60);

            // Act
            var projected = EqMapProjection.ProjectPlayer(location, offset);

            // Assert
            // (120-20, 260-60) = (100, 200) -> (-200, -100)
            Assert.AreEqual(-200, projected.X);
            Assert.AreEqual(-100, projected.Y);
        }

        [TestMethod]
        public void ProjectPlayer_ZeroOffset_MatchesAPlainProjection()
        {
            // Arrange
            var location = new EqMapProjection.Point3DLike(33, 77);
            var zero = new EqMapProjection.Point3DLike(0, 0);

            // Act
            var viaPlayer = EqMapProjection.ProjectPlayer(location, zero);
            var direct = EqMapProjection.ToScreenSpace(33, 77);

            // Assert
            Assert.AreEqual(direct, viaPlayer);
        }

        private static ParsedData DataWithLine(double x1, double y1, double x2, double y2)
        {
            return new ParsedData
            {
                Lines = new List<MapLine>
                {
                    new MapLine
                    {
                        Points = new[]
                        {
                            new Point3D { X = x1, Y = y1, Z = 0 },
                            new Point3D { X = x2, Y = y2, Z = 0 }
                        }
                    }
                }
            };
        }

        [TestMethod]
        public void ComputeTransformedBounds_SingleLine_SpansBothProjectedPoints()
        {
            // Arrange
            // (0,0) -> (0,0) and (100,200) -> (-200,-100), so the box runs from
            // (-200,-100) to (0,0).
            var data = DataWithLine(0, 0, 100, 200);

            // Act
            var bounds = EqMapProjection.ComputeTransformedBounds(data);

            // Assert
            Assert.AreEqual(-200, bounds.X);
            Assert.AreEqual(-100, bounds.Y);
            Assert.AreEqual(200, bounds.Width);
            Assert.AreEqual(100, bounds.Height);
        }

        [TestMethod]
        public void ComputeTransformedBounds_NoData_ReturnsEmptyRatherThanThrowing()
        {
            // Assert
            Assert.AreEqual(default, EqMapProjection.ComputeTransformedBounds(null));
            Assert.AreEqual(default, EqMapProjection.ComputeTransformedBounds(new ParsedData()));
        }

        [TestMethod]
        public void ComputeTransformedBounds_LineWithNullPoints_IsSkipped()
        {
            // Arrange
            // Map files are third-party data; a malformed line must not take the
            // whole zone down.
            var data = DataWithLine(0, 0, 100, 200);
            data.Lines.Add(new MapLine { Points = null });

            // Act
            var bounds = EqMapProjection.ComputeTransformedBounds(data);

            // Assert
            Assert.AreEqual(200, bounds.Width);
            Assert.AreEqual(100, bounds.Height);
        }

        [TestMethod]
        public void ComputeTransformedBounds_RealZoneScale_ProducesAPositiveExtent()
        {
            // Arrange
            // Plane of Fear runs to a few thousand units across.
            var data = DataWithLine(-1500, -2000, 1500, 2000);

            // Act
            var bounds = EqMapProjection.ComputeTransformedBounds(data);

            // Assert
            Assert.AreEqual(4000, bounds.Width);
            Assert.AreEqual(3000, bounds.Height);
        }
    }
}
