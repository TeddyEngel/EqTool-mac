using EQTool.Services;
using EQTool.Services.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace EQTool.Core.Tests
{
    // The client reads the log by polling every 100ms and asking FileReader for
    // whatever is new. FileReader keeps the offset it stopped at, so "new" means
    // since the last call rather than since the file began.
    //
    // Upstream covers a single read. Nothing covered reading twice, which is the
    // only thing the running client ever does. If the offset stopped advancing,
    // every poll would hand the same lines back and every trigger in them would
    // fire ten times a second, which is the sort of failure that looks like the
    // parser being wrong rather than the reader.
    //
    // These run against a temporary directory rather than the configured Wine
    // prefix, so nothing here touches a real log.
    [TestClass]
    public class LogTailingTests
    {
        private string logDirectory;
        private string logFile;

        [TestInitialize]
        public void Setup()
        {
            logDirectory = Path.Combine(Path.GetTempPath(), "pigparse-tail-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(logDirectory);
            logFile = Path.Combine(logDirectory, "eqlog_Sisytest_P1999Green.txt");
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(logDirectory))
                    Directory.Delete(logDirectory, recursive: true);
            }
            catch
            {
            }
        }

        private static string Line(string body)
        {
            return "[Mon Sep 01 12:00:00 2025] " + body;
        }

        private void Append(string body)
        {
            File.AppendAllText(logFile, Line(body) + Environment.NewLine);
        }

        [TestMethod]
        public void ReadNext_SecondCallAfterAnAppend_ReturnsOnlyTheNewLine()
        {
            // Arrange
            Append("Vebanab slices a willowisp for 56 points of damage.");
            var reader = new FileReader();
            _ = reader.ReadNext(logFile);

            // Act
            Append("Ratman Rager was hit by non-melee for 45 points of damage.");
            var second = reader.ReadNext(logFile);

            // Assert
            Assert.AreEqual(1, second.Count, "The reader handed back more than the newly appended line.");
            StringAssert.Contains(second[0], "Ratman Rager");
        }

        [TestMethod]
        public void ReadNext_WithNothingAppended_ReturnsNothing()
        {
            // Arrange
            // A poll every 100ms means this is the common case by a wide margin.
            Append("Vebanab slices a willowisp for 56 points of damage.");
            var reader = new FileReader();
            _ = reader.ReadNext(logFile);

            // Act
            var second = reader.ReadNext(logFile);

            // Assert
            Assert.AreEqual(0, second.Count, "An unchanged file produced lines, so they would be reprocessed every poll.");
        }

        [TestMethod]
        public void ReadNext_AcrossManyPolls_NeverRepeatsALine()
        {
            // Arrange
            // A trigger firing on a repeated line is indistinguishable from the
            // trigger being wrong, so this walks several appends rather than one.
            var reader = new FileReader();
            Append("Welcome to EverQuest!");
            _ = reader.ReadNext(logFile);
            var seen = 0;

            // Act
            for (var i = 0; i < 5; i++)
            {
                Append($"Vebanab slices a willowisp for {i} points of damage.");
                var lines = reader.ReadNext(logFile);
                seen += lines.Count;

                // Assert
                Assert.AreEqual(1, lines.Count, $"Poll {i} returned {lines.Count} lines instead of the one appended.");
            }

            Assert.AreEqual(5, seen);
        }

        [TestMethod]
        public void ReadNext_WhenTheFileIsReplacedByAShorterOne_DoesNotThrowOrStall()
        {
            // Arrange
            // Archiving moves the log aside and EverQuest starts a new one, so the
            // path stays and the file shrinks underneath the reader.
            Append("Vebanab slices a willowisp for 56 points of damage.");
            var reader = new FileReader();
            _ = reader.ReadNext(logFile);

            // Act
            File.WriteAllText(logFile, Line("Welcome to EverQuest!") + Environment.NewLine);
            var afterRotation = reader.ReadNext(logFile);

            // Assert
            Assert.IsNotNull(afterRotation);
            Append("Ratman Rager was hit by non-melee for 45 points of damage.");
            var next = reader.ReadNext(logFile);
            Assert.IsTrue(
                next.Any(line => line.Contains("Ratman Rager")),
                "The reader stopped following the file after it was rotated.");
        }

        [TestMethod]
        public void ReadNext_WhenTheCharacterChanges_FollowsTheNewFile()
        {
            // Arrange
            // Switching character switches log file, and the reader keys its
            // offset by path.
            Append("Vebanab slices a willowisp for 56 points of damage.");
            var reader = new FileReader();
            _ = reader.ReadNext(logFile);

            var otherFile = Path.Combine(logDirectory, "eqlog_Otherchar_P1999Green.txt");
            File.WriteAllText(otherFile, Line("Welcome to EverQuest!") + Environment.NewLine);

            // Act
            var fromOther = reader.ReadNext(otherFile);

            // Assert
            Assert.IsTrue(
                fromOther.Any(line => line.Contains("Welcome to EverQuest!")),
                "Switching to another character's log returned nothing.");
        }
    }
}
