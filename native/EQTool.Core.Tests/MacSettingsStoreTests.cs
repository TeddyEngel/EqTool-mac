using EQTool.Core.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class MacSettingsStoreTests
    {
        private string testRoot;
        private string executableDirectory;
        private string canonicalDirectory;

        [TestInitialize]
        public void CreateTemporaryDirectories()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "pigparse-settings-" + Path.GetRandomFileName());
            executableDirectory = Path.Combine(testRoot, "bin");
            canonicalDirectory = Path.Combine(testRoot, "ApplicationSupport");
            _ = Directory.CreateDirectory(executableDirectory);
        }

        [TestCleanup]
        public void RemoveTemporaryDirectories()
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }

        private string LinkPath => Path.Combine(executableDirectory, MacSettingsStore.SettingsFileName);

        private string CanonicalPath => Path.Combine(canonicalDirectory, MacSettingsStore.SettingsFileName);

        [TestMethod]
        public void EnsureRedirected_NoExistingSettings_CreatesLinkToCanonicalPath()
        {
            // Act
            var canonicalPath = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);

            // Assert
            Assert.AreEqual(CanonicalPath, canonicalPath);
            Assert.IsTrue(Directory.Exists(canonicalDirectory));
            Assert.AreEqual(CanonicalPath, File.ResolveLinkTarget(LinkPath, returnFinalTarget: false).FullName);
        }

        [TestMethod]
        public void EnsureRedirected_WriteThroughLink_LandsInCanonicalFile()
        {
            // Arrange
            _ = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);

            // Act
            File.WriteAllText(LinkPath, "{\"DefaultEqDirectory\":\"/Users/someone/EQ\"}");

            // Assert
            Assert.IsTrue(File.Exists(CanonicalPath));
            StringAssert.Contains(File.ReadAllText(CanonicalPath), "/Users/someone/EQ");
        }

        [TestMethod]
        public void EnsureRedirected_ExistingSettingsInBuildOutput_MigratesContentToCanonical()
        {
            // Arrange
            File.WriteAllText(LinkPath, "{\"DefaultEqDirectory\":\"/Users/someone/Existing\"}");

            // Act
            _ = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);

            // Assert
            StringAssert.Contains(File.ReadAllText(CanonicalPath), "/Users/someone/Existing");
            Assert.AreEqual(CanonicalPath, File.ResolveLinkTarget(LinkPath, returnFinalTarget: false).FullName);
        }

        [TestMethod]
        public void EnsureRedirected_CanonicalAlreadyExists_DoesNotOverwriteIt()
        {
            // Arrange
            _ = Directory.CreateDirectory(canonicalDirectory);
            File.WriteAllText(CanonicalPath, "{\"DefaultEqDirectory\":\"/Users/someone/Canonical\"}");
            File.WriteAllText(LinkPath, "{\"DefaultEqDirectory\":\"/Users/someone/Stale\"}");

            // Act
            _ = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);

            // Assert
            StringAssert.Contains(File.ReadAllText(CanonicalPath), "/Users/someone/Canonical");
        }

        [TestMethod]
        public void EnsureRedirected_CanonicalAlreadyExists_SetsTheOtherFileAsideRatherThanDeletingIt()
        {
            // Arrange
            // Two real files: one in the build output and one already canonical.
            // The canonical one wins, but the other is a real configuration and
            // the redirect has to clear that path to put the symlink there.
            _ = Directory.CreateDirectory(canonicalDirectory);
            File.WriteAllText(CanonicalPath, "{\"DefaultEqDirectory\":\"/Users/someone/Canonical\"}");
            File.WriteAllText(LinkPath, "{\"DefaultEqDirectory\":\"/Users/someone/Superseded\"}");

            // Act
            _ = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);

            // Assert
            var supersededPath = CanonicalPath + MacSettingsStore.SupersededSuffix;
            Assert.IsTrue(File.Exists(supersededPath), "The superseded settings file was deleted rather than kept.");
            StringAssert.Contains(File.ReadAllText(supersededPath), "/Users/someone/Superseded");
            StringAssert.Contains(File.ReadAllText(CanonicalPath), "/Users/someone/Canonical");
        }

        [TestMethod]
        public void EnsureRedirected_CalledTwice_RemainsLinkedAndPreservesContent()
        {
            // Arrange
            _ = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);
            File.WriteAllText(LinkPath, "{\"DefaultEqDirectory\":\"/Users/someone/Kept\"}");

            // Act
            _ = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);

            // Assert
            Assert.AreEqual(CanonicalPath, File.ResolveLinkTarget(LinkPath, returnFinalTarget: false).FullName);
            StringAssert.Contains(File.ReadAllText(CanonicalPath), "/Users/someone/Kept");
        }

        [TestMethod]
        public void EnsureRedirected_AfterBuildOutputWiped_RestoresLinkWithSettingsIntact()
        {
            // Arrange
            _ = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);
            File.WriteAllText(LinkPath, "{\"DefaultEqDirectory\":\"/Users/someone/Survives\"}");

            // Act - simulate dotnet clean removing the whole build output
            Directory.Delete(executableDirectory, recursive: true);
            _ = Directory.CreateDirectory(executableDirectory);
            _ = MacSettingsStore.EnsureRedirected(executableDirectory, canonicalDirectory);

            // Assert
            StringAssert.Contains(File.ReadAllText(LinkPath), "/Users/someone/Survives");
        }
    }
}
