using System;
using EQTool.Services;
using EQTool.Services.Parsing;
using EQTool.ViewModels;
using EQTool.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class DamageParserTests
    {
        private DamageParser BuildParser()
        {
            var logEvents = new LogEvents();
            var settings = new EQToolSettings();
            var activePlayer = new ActivePlayer(settings, logEvents);
            return new DamageParser(activePlayer, logEvents);
        }

        [TestMethod]
        public void Match_YouCrushForDamage_ExtractsAttackerTargetAndAmount()
        {
            var parser = BuildParser();
            var timestamp = new DateTime(2026, 9, 4, 12, 0, 0);
            var line = "You crush a giant wasp drone for 12 points of damage.";

            var damageEvent = parser.Match(line, timestamp, 42);

            Assert.IsNotNull(damageEvent);
            Assert.AreEqual("You", damageEvent.AttackerName);
            Assert.AreEqual("crush", damageEvent.DamageType);
            Assert.AreEqual("a giant wasp drone", damageEvent.TargetName);
            Assert.AreEqual(12, damageEvent.DamageDone);
        }

        [TestMethod]
        public void Match_OtherHitsForDamage_ExtractsAttackerTargetAndAmount()
        {
            var parser = BuildParser();
            var line = "Vebanab slices a willowisp for 56 points of damage.";

            var damageEvent = parser.Match(line, DateTime.UtcNow, 0);

            Assert.IsNotNull(damageEvent);
            Assert.AreEqual("Vebanab", damageEvent.AttackerName);
            Assert.AreEqual("slices", damageEvent.DamageType);
            Assert.AreEqual("a willowisp", damageEvent.TargetName);
            Assert.AreEqual(56, damageEvent.DamageDone);
        }

        [TestMethod]
        public void Match_NonMelee_TargetIsHitPersonAttackerIsYou()
        {
            var parser = BuildParser();
            var line = "Ratman Rager was hit by non-melee for 45 points of damage.";

            var damageEvent = parser.Match(line, DateTime.UtcNow, 0);

            Assert.IsNotNull(damageEvent);
            Assert.AreEqual("You", damageEvent.AttackerName);
            Assert.AreEqual("non-melee", damageEvent.DamageType);
            Assert.AreEqual("Ratman Rager", damageEvent.TargetName);
            Assert.AreEqual(45, damageEvent.DamageDone);
        }

        [TestMethod]
        public void Match_YouTryButMiss_ExtractsAttackAndTargetWithZeroDamage()
        {
            var parser = BuildParser();
            var line = "You try to pierce an Iksar outcast, but miss!";

            var damageEvent = parser.Match(line, DateTime.UtcNow, 0);

            Assert.IsNotNull(damageEvent);
            Assert.AreEqual("You", damageEvent.AttackerName);
            Assert.AreEqual("pierce", damageEvent.DamageType);
            Assert.AreEqual("an Iksar outcast", damageEvent.TargetName);
            Assert.AreEqual(0, damageEvent.DamageDone);
        }

        [TestMethod]
        public void Match_UnrelatedLine_ReturnsNull()
        {
            var parser = BuildParser();
            var line = "You have entered East Commonlands.";

            var damageEvent = parser.Match(line, DateTime.UtcNow, 0);

            Assert.IsNull(damageEvent);
        }
    }
}
