using EQTool.Core.Platform;
using EQToolShared;
using EQToolShared.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class MacPathNormalizerTests
    {
        private const string WinePrefix = "/Users/someone/.wine-pigparse";

        [TestMethod]
        public void TryNormalize_NativePosixPath_ReturnsPathUnchanged()
        {
            // Arrange
            var path = "/Users/someone/EverQuest/Logs";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, WinePrefix, out var normalized);

            // Assert
            Assert.IsTrue(succeeded);
            Assert.AreEqual("/Users/someone/EverQuest/Logs", normalized);
        }

        [TestMethod]
        public void TryNormalize_HostRootDrive_MapsToFilesystemRoot()
        {
            // Arrange
            var path = @"Z:\Users\someone\EverQuest";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, WinePrefix, out var normalized);

            // Assert
            Assert.IsTrue(succeeded);
            Assert.AreEqual("/Users/someone/EverQuest", normalized);
        }

        [TestMethod]
        public void TryNormalize_CDriveWithPrefix_MapsIntoDriveC()
        {
            // Arrange
            var path = @"C:\EQ\Logs";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, WinePrefix, out var normalized);

            // Assert
            Assert.IsTrue(succeeded);
            Assert.AreEqual("/Users/someone/.wine-pigparse/drive_c/EQ/Logs", normalized);
        }

        [TestMethod]
        public void TryNormalize_LowercaseDriveLetter_MapsSameAsUppercase()
        {
            // Arrange
            var path = @"c:\EQ\Logs";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, WinePrefix, out var normalized);

            // Assert
            Assert.IsTrue(succeeded);
            Assert.AreEqual("/Users/someone/.wine-pigparse/drive_c/EQ/Logs", normalized);
        }

        [TestMethod]
        public void TryNormalize_BareDriveRoot_MapsToDriveDirectory()
        {
            // Arrange
            var path = @"C:\";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, WinePrefix, out var normalized);

            // Assert
            Assert.IsTrue(succeeded);
            Assert.AreEqual("/Users/someone/.wine-pigparse/drive_c", normalized);
        }

        [TestMethod]
        public void TryNormalize_CDriveWithoutPrefix_FailsRatherThanGuessing()
        {
            // Arrange
            var path = @"C:\EQ\Logs";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, null, out var normalized);

            // Assert
            Assert.IsFalse(succeeded);
            Assert.IsNull(normalized);
        }

        [TestMethod]
        public void TryNormalize_RelativeWindowsPath_ConvertsSeparators()
        {
            // Arrange
            var path = @"Logs\eqlog_Pigy_P1999Green.txt";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, WinePrefix, out var normalized);

            // Assert
            Assert.IsTrue(succeeded);
            Assert.AreEqual("Logs/eqlog_Pigy_P1999Green.txt", normalized);
        }

        [TestMethod]
        public void TryNormalize_EmptyPath_ReturnsFalse()
        {
            // Arrange
            var path = "   ";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, WinePrefix, out var normalized);

            // Assert
            Assert.IsFalse(succeeded);
            Assert.IsNull(normalized);
        }

        [TestMethod]
        public void TryNormalize_TrailingPrefixSeparator_DoesNotDoubleUp()
        {
            // Arrange
            var path = @"C:\EQ";

            // Act
            var succeeded = MacPathNormalizer.TryNormalize(path, WinePrefix + "/", out var normalized);

            // Assert
            Assert.IsTrue(succeeded);
            Assert.AreEqual("/Users/someone/.wine-pigparse/drive_c/EQ", normalized);
        }

        // The two tests below are why this class exists. They pin the upstream
        // behaviour that motivated it: both helpers are correct once a path is
        // normalised, and both corrupt Windows input silently if it is not.

        [TestMethod]
        public void PathsCombine_AfterNormalization_ProducesUsableMacPath()
        {
            // Arrange
            MacPathNormalizer.TryNormalize(@"C:\EQ\", WinePrefix, out var eqDirectory);

            // Act
            var combined = Paths.Combine(eqDirectory, "eqclient.ini");

            // Assert
            Assert.AreEqual("/Users/someone/.wine-pigparse/drive_c/EQ/eqclient.ini", combined);
        }

        [TestMethod]
        public void UIFileNameTryParse_AfterNormalization_ExtractsPlayerNameCorrectly()
        {
            // Arrange
            MacPathNormalizer.TryNormalize(@"C:\EQ\UI_Pigy_P1999Green.ini", WinePrefix, out var uiFilePath);

            // Act
            var succeeded = UIFileName.TryParse(uiFilePath, out var info);

            // Assert
            Assert.IsTrue(succeeded);
            Assert.AreEqual("Pigy", info.PlayerName);
            Assert.IsTrue(info.IsUiLayoutFile);
        }
    }
}
