using EQTool.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class EqGameFocusTests
    {
        private Func<int?> savedFrontmost;
        private Func<int, string> savedResolver;

        [TestInitialize]
        public void Setup()
        {
            savedFrontmost = EqGameFocus.FrontmostProcessId;
            savedResolver = EqGameFocus.ResolveProcessName;
        }

        [TestCleanup]
        public void Restore()
        {
            EqGameFocus.FrontmostProcessId = savedFrontmost;
            EqGameFocus.ResolveProcessName = savedResolver;
        }

        [TestMethod]
        public void NormalizeProcessName_WithExeExtension_DropsIt()
        {
            // Act
            var name = EqGameFocus.NormalizeProcessName("eqgame.exe");

            // Assert
            Assert.AreEqual("eqgame", name);
        }

        [TestMethod]
        public void NormalizeProcessName_WithWindowsPath_KeepsOnlyTheExecutable()
        {
            // Act
            var name = EqGameFocus.NormalizeProcessName(@"C:\windows\system32\explorer.exe");

            // Assert
            Assert.AreEqual("explorer", name);
        }

        [TestMethod]
        public void NormalizeProcessName_WithUnixPath_KeepsOnlyTheExecutable()
        {
            // Act
            var name = EqGameFocus.NormalizeProcessName("/Users/someone/eqgame.exe");

            // Assert
            Assert.AreEqual("eqgame", name);
        }

        [TestMethod]
        public void NormalizeProcessName_WithBareName_LeavesItAlone()
        {
            // Act
            var name = EqGameFocus.NormalizeProcessName("eqgame");

            // Assert
            Assert.AreEqual("eqgame", name);
        }

        [TestMethod]
        public void NormalizeProcessName_WithNothing_ReturnsEmpty()
        {
            // Assert
            Assert.AreEqual(string.Empty, EqGameFocus.NormalizeProcessName(null));
            Assert.AreEqual(string.Empty, EqGameFocus.NormalizeProcessName("   "));
        }

        [TestMethod]
        public void IsEqGame_IgnoresCase()
        {
            // Assert
            Assert.IsTrue(EqGameFocus.IsEqGame("EQGame.EXE"));
        }

        [TestMethod]
        public void IsEqGame_ForTheWineWrapper_IsFalse()
        {
            // NSWorkspace names every Wine-hosted program "wine". Matching it would
            // treat any Wine app as EverQuest.
            Assert.IsFalse(EqGameFocus.IsEqGame("wine"));
        }

        [TestMethod]
        public void IsEqGame_ForAnotherWineProgram_IsFalse()
        {
            // Assert
            Assert.IsFalse(EqGameFocus.IsEqGame("notepad.exe"));
        }

        [TestMethod]
        public void IsFocused_WithNoProbeInstalled_IsFalse()
        {
            // Arrange
            EqGameFocus.FrontmostProcessId = null;

            // Assert
            Assert.IsFalse(EqGameFocus.IsFocused());
        }

        [TestMethod]
        public void IsFocused_WhenNothingIsFrontmost_IsFalse()
        {
            // Arrange
            EqGameFocus.FrontmostProcessId = () => null;

            // Assert
            Assert.IsFalse(EqGameFocus.IsFocused());
        }

        [TestMethod]
        public void IsFocused_WhenTheFrontmostProcessIsEqGame_IsTrue()
        {
            // Arrange
            EqGameFocus.FrontmostProcessId = () => 4242;
            EqGameFocus.ResolveProcessName = pid => pid == 4242 ? "eqgame.exe" : "other";

            // Assert
            Assert.IsTrue(EqGameFocus.IsFocused());
        }

        [TestMethod]
        public void IsFocused_WhenTheFrontmostProcessIsSomethingElse_IsFalse()
        {
            // Arrange
            EqGameFocus.FrontmostProcessId = () => 4242;
            EqGameFocus.ResolveProcessName = _ => "notepad.exe";

            // Assert
            Assert.IsFalse(EqGameFocus.IsFocused());
        }

        [TestMethod]
        public void IsFocused_WhenTheProbeThrows_IsFalse()
        {
            // Arrange
            EqGameFocus.FrontmostProcessId = () => throw new InvalidOperationException("boom");

            // Assert
            Assert.IsFalse(EqGameFocus.IsFocused());
        }

        [TestMethod]
        public void NativeProcessName_ForThisProcess_ReturnsSomething()
        {
            // Act
            var name = EqGameFocus.NativeProcessName(Environment.ProcessId);

            // Assert
            Assert.IsFalse(string.IsNullOrWhiteSpace(name), "proc_name should resolve the running test host.");
        }
    }
}
