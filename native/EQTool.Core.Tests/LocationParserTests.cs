using System;
using System.Windows.Media.Media3D;
using EQTool.Models;
using EQTool.Services;
using EQTool.Services.Parsing;
using EQTool.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class LocationParserTests
    {
        private static (LocationParser parser, LogEvents logEvents) BuildParser()
        {
            var logEvents = new LogEvents();
            var settings = new EQToolSettings();
            var activePlayer = new ActivePlayer(settings, logEvents);
            var parser = new LocationParser(logEvents, activePlayer);
            return (parser, logEvents);
        }

        [TestMethod]
        public void Handle_YourLocationLine_RaisesPlayerLocationEventWithParsedPoint3D()
        {
            var (parser, logEvents) = BuildParser();
            PlayerLocationEvent captured = null;
            logEvents.PlayerLocationEvent += (_, e) => captured = e;

            var timestamp = new DateTime(2026, 9, 4, 12, 0, 0);
            var handled = parser.Handle("Your Location is 123.45, -678.90, 42.00", timestamp, 7);

            Assert.IsTrue(handled);
            Assert.IsNotNull(captured);
            Assert.AreEqual(123.45, Math.Round(captured.Location.X, 2));
            Assert.AreEqual(-678.90, Math.Round(captured.Location.Y, 2));
            Assert.AreEqual(42.00, Math.Round(captured.Location.Z, 2));
            Assert.AreEqual(timestamp, captured.TimeStamp);
            Assert.AreEqual(7, captured.LineCounter);
        }

        [TestMethod]
        public void Handle_UnrelatedLine_ReturnsFalseAndRaisesNothing()
        {
            var (parser, logEvents) = BuildParser();
            var fired = false;
            logEvents.PlayerLocationEvent += (_, _) => fired = true;

            var handled = parser.Handle("You have entered East Commonlands.", DateTime.UtcNow, 0);

            Assert.IsFalse(handled);
            Assert.IsFalse(fired);
        }

        [TestMethod]
        public void Point3DShim_ObjectInitializer_PreservesXYZ()
        {
            var point = new Point3D { X = 1.5, Y = -2.5, Z = 3.0 };

            Assert.AreEqual(1.5, point.X);
            Assert.AreEqual(-2.5, point.Y);
            Assert.AreEqual(3.0, point.Z);
        }

        [TestMethod]
        public void Point3DShim_NullableUsage_MatchesUpstreamNullableValueTypeContract()
        {
            Point3D? maybe = null;
            Assert.IsFalse(maybe.HasValue);

            maybe = new Point3D { X = 10, Y = 20, Z = 30 };
            Assert.IsTrue(maybe.HasValue);
            Assert.AreEqual(20, maybe.Value.Y);
        }
    }
}
