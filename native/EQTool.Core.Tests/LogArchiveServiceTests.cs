using EQTool.Models;
using EQTool.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace EQTool.Core.Tests
{
    // The checkbox for this was in the settings window from the start, but the
    // service behind it was never compiled into the Mac build, so ticking it did
    // nothing. These cover it now that it is wired up.
    //
    // Everything here runs against a temporary directory. The service moves real
    // log files, so pointing it at a live EverQuest folder from a test would
    // relocate them.
    [TestClass]
    public class LogArchiveServiceTests
    {
        private string logDirectory;

        [TestInitialize]
        public void Setup()
        {
            logDirectory = Path.Combine(Path.GetTempPath(), "pigparse-archive-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(logDirectory);
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

        private string WriteLog(string name, int megabytes)
        {
            var path = Path.Combine(logDirectory, name);
            File.WriteAllBytes(path, new byte[megabytes * 1024 * 1024]);
            return path;
        }

        private EQToolSettings SettingsFor(bool enabled, int thresholdMegabytes)
        {
            return new EQToolSettings
            {
                EqLogDirectory = logDirectory,
                LogArchiveEnabled = enabled,
                LogArchiveSizeMB = thresholdMegabytes,
            };
        }

        private string ArchiveDirectory => Path.Combine(logDirectory, "archive");

        [TestMethod]
        public void TryArchiveLogs_WhenDisabled_LeavesFilesAlone()
        {
            // Arrange
            // The setting defaults to off, so this is the state almost every
            // install is in. Moving a log here would be moving files nobody
            // asked to have moved.
            var log = WriteLog("eqlog_Pigy_P1999Green.txt", 2);
            using var service = new LogArchiveService(SettingsFor(enabled: false, thresholdMegabytes: 1));

            // Act
            service.TryArchiveLogs();

            // Assert
            Assert.IsTrue(File.Exists(log));
            Assert.IsFalse(Directory.Exists(ArchiveDirectory));
        }

        [TestMethod]
        public void TryArchiveLogs_WhenEnabledAndOverThreshold_MovesTheFile()
        {
            // Arrange
            var log = WriteLog("eqlog_Pigy_P1999Green.txt", 2);
            using var service = new LogArchiveService(SettingsFor(enabled: true, thresholdMegabytes: 1));

            // Act
            service.TryArchiveLogs();

            // Assert
            Assert.IsFalse(File.Exists(log), "The oversized log should have been moved out of the log directory.");
            Assert.IsTrue(Directory.Exists(ArchiveDirectory));
            Assert.AreEqual(1, Directory.GetFiles(ArchiveDirectory).Length);
        }

        [TestMethod]
        public void TryArchiveLogs_WhenEnabledAndUnderThreshold_LeavesTheFile()
        {
            // Arrange
            var log = WriteLog("eqlog_Pigy_P1999Green.txt", 1);
            using var service = new LogArchiveService(SettingsFor(enabled: true, thresholdMegabytes: 50));

            // Act
            service.TryArchiveLogs();

            // Assert
            Assert.IsTrue(File.Exists(log));
        }

        [TestMethod]
        public void TryArchiveLogs_KeepsTheOriginalNameInTheArchivedFile()
        {
            // Arrange
            // The archived name carries a timestamp, so the check is that the
            // character and server can still be read off it.
            _ = WriteLog("eqlog_Pigy_P1999Green.txt", 2);
            using var service = new LogArchiveService(SettingsFor(enabled: true, thresholdMegabytes: 1));

            // Act
            service.TryArchiveLogs();

            // Assert
            var archived = Path.GetFileName(Directory.GetFiles(ArchiveDirectory).Single());
            StringAssert.StartsWith(archived, "eqlog_Pigy_P1999Green_");
            StringAssert.EndsWith(archived, ".txt");
        }

        [TestMethod]
        public void TryArchiveLogs_WithAMissingDirectory_DoesNotThrow()
        {
            // Arrange
            // The log directory is whatever the settings say, and it can be stale
            // or unset on a fresh install.
            var settings = SettingsFor(enabled: true, thresholdMegabytes: 1);
            settings.EqLogDirectory = Path.Combine(logDirectory, "gone");
            using var service = new LogArchiveService(settings);

            // Act
            service.TryArchiveLogs();

            // Assert
            Assert.IsFalse(Directory.Exists(ArchiveDirectory));
        }

        [TestMethod]
        public void TryArchiveLogs_OnlyTouchesTextFiles()
        {
            // Arrange
            // The log folder holds more than logs, and the settings file itself
            // lives alongside them on some installs.
            var other = Path.Combine(logDirectory, "settings.json");
            File.WriteAllBytes(other, new byte[2 * 1024 * 1024]);
            using var service = new LogArchiveService(SettingsFor(enabled: true, thresholdMegabytes: 1));

            // Act
            service.TryArchiveLogs();

            // Assert
            Assert.IsTrue(File.Exists(other));
        }
    }
}
