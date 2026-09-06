using EQTool.Core.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class EqLoggingEnablerTests
    {
        private string directory;

        [TestInitialize]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "pigparse-logtest-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(directory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        private string WriteConfig(params string[] lines)
        {
            var path = Path.Combine(directory, EqLoggingEnabler.ConfigFileName);
            File.WriteAllLines(path, lines);
            return path;
        }

        [TestMethod]
        public void Enable_WithLogFalse_RewritesItToTrue()
        {
            // Arrange
            var path = WriteConfig("[Defaults]", "Log=FALSE", "Vsync=1");

            // Act
            var result = EqLoggingEnabler.Enable(directory);

            // Assert
            Assert.AreEqual(EqLoggingEnableOutcome.Enabled, result.Outcome);
            CollectionAssert.AreEqual(
                new[] { "[Defaults]", "Log=TRUE", "Vsync=1" },
                File.ReadAllLines(path));
        }

        [TestMethod]
        public void Enable_WithNoLogLineAtAll_AppendsOne()
        {
            // Arrange
            // Upstream only rewrites an existing line, so this file would have
            // been left with logging still off.
            var path = WriteConfig("[Defaults]", "Vsync=1");

            // Act
            var result = EqLoggingEnabler.Enable(directory);

            // Assert
            Assert.AreEqual(EqLoggingEnableOutcome.Enabled, result.Outcome);
            CollectionAssert.AreEqual(
                new[] { "[Defaults]", "Vsync=1", "Log=TRUE" },
                File.ReadAllLines(path));
        }

        [TestMethod]
        public void Enable_BacksUpTheOriginalFirst()
        {
            // Arrange
            var original = new[] { "[Defaults]", "Log=FALSE" };
            _ = WriteConfig(original);

            // Act
            var result = EqLoggingEnabler.Enable(directory);

            // Assert
            Assert.IsNotNull(result.BackupPath);
            Assert.IsTrue(File.Exists(result.BackupPath));
            CollectionAssert.AreEqual(original, File.ReadAllLines(result.BackupPath));
        }

        [TestMethod]
        public void Enable_WhenAlreadyOn_ChangesNothing()
        {
            // Arrange
            var path = WriteConfig("[Defaults]", "Log=TRUE");

            // Act
            var result = EqLoggingEnabler.Enable(directory);

            // Assert
            Assert.AreEqual(EqLoggingEnableOutcome.AlreadyEnabled, result.Outcome);
            Assert.IsNull(result.BackupPath);
            CollectionAssert.AreEqual(new[] { "[Defaults]", "Log=TRUE" }, File.ReadAllLines(path));
        }

        [TestMethod]
        [DataRow("log = false")]
        [DataRow("LOG=False")]
        [DataRow("  Log=0  ")]
        public void Enable_IgnoresSpacingAndCase(string logLine)
        {
            // Arrange
            var path = WriteConfig("[Defaults]", logLine);

            // Act
            var result = EqLoggingEnabler.Enable(directory);

            // Assert
            Assert.AreEqual(EqLoggingEnableOutcome.Enabled, result.Outcome);
            CollectionAssert.AreEqual(new[] { "[Defaults]", "Log=TRUE" }, File.ReadAllLines(path));
        }

        [TestMethod]
        public void Enable_WhenAlreadyOnWithOddSpacing_IsStillRecognised()
        {
            // Arrange
            _ = WriteConfig("[Defaults]", "  log = TRUE ");

            // Act
            var result = EqLoggingEnabler.Enable(directory);

            // Assert
            Assert.AreEqual(EqLoggingEnableOutcome.AlreadyEnabled, result.Outcome);
        }

        [TestMethod]
        public void Enable_WithNoConfigFile_SaysSo()
        {
            // Act
            var result = EqLoggingEnabler.Enable(directory);

            // Assert
            Assert.AreEqual(EqLoggingEnableOutcome.NoConfigFile, result.Outcome);
        }

        [TestMethod]
        public void Enable_WithNoDirectory_Fails()
        {
            // Assert
            Assert.AreEqual(EqLoggingEnableOutcome.Failed, EqLoggingEnabler.Enable(null).Outcome);
            Assert.AreEqual(EqLoggingEnableOutcome.Failed, EqLoggingEnabler.Enable("   ").Outcome);
        }
    }
}
