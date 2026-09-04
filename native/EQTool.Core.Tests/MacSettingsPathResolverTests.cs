using EQTool.Core.Platform;
using EQTool.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class MacSettingsPathResolverTests
    {
        private const string WinePrefix = "/Users/someone/.wine-pigparse";

        [TestMethod]
        public void Resolve_WindowsPathsFromWineBuild_RewritesBothDirectories()
        {
            // Arrange
            var settings = new EQToolSettings
            {
                DefaultEqDirectory = @"C:\EQ",
                EqLogDirectory = @"C:\EQ\Logs"
            };

            // Act
            var resolution = MacSettingsPathResolver.Resolve(settings, WinePrefix);

            // Assert
            Assert.IsTrue(resolution.AllResolved);
            Assert.AreEqual("/Users/someone/.wine-pigparse/drive_c/EQ", settings.DefaultEqDirectory);
            Assert.AreEqual("/Users/someone/.wine-pigparse/drive_c/EQ/Logs", settings.EqLogDirectory);
        }

        [TestMethod]
        public void Resolve_HostRootDrivePaths_MapsToFilesystemRoot()
        {
            // Arrange
            var settings = new EQToolSettings
            {
                DefaultEqDirectory = @"Z:\Users\someone\EverQuest",
                EqLogDirectory = @"Z:\Users\someone\EverQuest\Logs"
            };

            // Act
            var resolution = MacSettingsPathResolver.Resolve(settings, WinePrefix);

            // Assert
            Assert.IsTrue(resolution.AllResolved);
            Assert.AreEqual("/Users/someone/EverQuest", settings.DefaultEqDirectory);
            Assert.AreEqual("/Users/someone/EverQuest/Logs", settings.EqLogDirectory);
        }

        [TestMethod]
        public void Resolve_NativeMacPaths_LeavesThemUnchanged()
        {
            // Arrange
            var settings = new EQToolSettings
            {
                DefaultEqDirectory = "/Users/someone/EverQuest",
                EqLogDirectory = "/Users/someone/EverQuest/Logs"
            };

            // Act
            var resolution = MacSettingsPathResolver.Resolve(settings, WinePrefix);

            // Assert
            Assert.IsTrue(resolution.AllResolved);
            Assert.AreEqual("/Users/someone/EverQuest", settings.DefaultEqDirectory);
            Assert.AreEqual("/Users/someone/EverQuest/Logs", settings.EqLogDirectory);
        }

        [TestMethod]
        public void Resolve_DriveLetterWithNoWinePrefix_LeavesOriginalUntouched()
        {
            // Arrange
            var settings = new EQToolSettings
            {
                DefaultEqDirectory = @"C:\EQ",
                EqLogDirectory = @"C:\EQ\Logs"
            };

            // Act
            var resolution = MacSettingsPathResolver.Resolve(settings, null);

            // Assert
            Assert.IsFalse(resolution.AllResolved);
            Assert.IsFalse(resolution.DefaultEqDirectoryResolved);
            Assert.IsFalse(resolution.EqLogDirectoryResolved);
            Assert.AreEqual(@"C:\EQ", settings.DefaultEqDirectory);
            Assert.AreEqual(@"C:\EQ\Logs", settings.EqLogDirectory);
        }

        [TestMethod]
        public void Resolve_OnlyOneDirectoryResolvable_ReportsPerDirectoryOutcome()
        {
            // Arrange
            var settings = new EQToolSettings
            {
                DefaultEqDirectory = @"Z:\Users\someone\EverQuest",
                EqLogDirectory = @"C:\EQ\Logs"
            };

            // Act
            var resolution = MacSettingsPathResolver.Resolve(settings, null);

            // Assert
            Assert.IsTrue(resolution.DefaultEqDirectoryResolved);
            Assert.IsFalse(resolution.EqLogDirectoryResolved);
            Assert.IsFalse(resolution.AllResolved);
            Assert.AreEqual("/Users/someone/EverQuest", settings.DefaultEqDirectory);
            Assert.AreEqual(@"C:\EQ\Logs", settings.EqLogDirectory);
        }

        [TestMethod]
        public void Resolve_UnsetDirectories_TreatedAsResolved()
        {
            // Arrange
            var settings = new EQToolSettings
            {
                DefaultEqDirectory = null,
                EqLogDirectory = "   "
            };

            // Act
            var resolution = MacSettingsPathResolver.Resolve(settings, WinePrefix);

            // Assert
            Assert.IsTrue(resolution.AllResolved);
            Assert.IsNull(settings.DefaultEqDirectory);
            Assert.AreEqual("   ", settings.EqLogDirectory);
        }

        [TestMethod]
        public void Resolve_NullSettings_DoesNotThrow()
        {
            // Arrange
            EQToolSettings settings = null;

            // Act
            var resolution = MacSettingsPathResolver.Resolve(settings, WinePrefix);

            // Assert
            Assert.IsFalse(resolution.AllResolved);
        }
    }
}
