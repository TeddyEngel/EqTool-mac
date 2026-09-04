using System;
using EQTool.Models;
using EQTool.Services;
using EQTool.Services.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class YouZonedParserTests
    {
        [TestMethod]
        public void ZoneChanged_YouHaveEnteredTempleOfVeeshan_ReturnsMappedShortName()
        {
            var parser = new YouZonedParser(new LogEvents());

            var zoneEvent = parser.ZoneChanged("You have entered Temple of Veeshan.", new DateTime(2026, 9, 4), 1);

            Assert.IsNotNull(zoneEvent);
            Assert.AreEqual("temple of veeshan", zoneEvent.LongName);
            Assert.AreEqual("templeveeshan", zoneEvent.ShortName);
        }

        [TestMethod]
        public void ZoneChanged_ArenaPvpNotice_ReturnsNull()
        {
            var parser = new YouZonedParser(new LogEvents());

            var zoneEvent = parser.ZoneChanged("You have entered an Arena (PvP) area.", DateTime.UtcNow, 0);

            Assert.IsNull(zoneEvent);
        }

        [TestMethod]
        public void ZoneChanged_UnrelatedLine_ReturnsNull()
        {
            var parser = new YouZonedParser(new LogEvents());

            var zoneEvent = parser.ZoneChanged("Your Location is 1, 2, 3", DateTime.UtcNow, 0);

            Assert.IsNull(zoneEvent);
        }

        [TestMethod]
        public void Handle_YouHaveEnteredZone_RaisesYouZonedEvent()
        {
            var logEvents = new LogEvents();
            YouZonedEvent captured = null;
            logEvents.YouZonedEvent += (_, e) => captured = e;
            var parser = new YouZonedParser(logEvents);

            var handled = parser.Handle("You have entered Temple of Veeshan.", DateTime.UtcNow, 5);

            Assert.IsTrue(handled);
            Assert.IsNotNull(captured);
            Assert.AreEqual("templeveeshan", captured.ShortName);
            Assert.AreEqual(5, captured.LineCounter);
        }
    }
}
