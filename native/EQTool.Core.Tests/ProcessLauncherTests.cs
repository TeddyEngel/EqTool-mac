using EQTool.Core.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace EQTool.Core.Tests
{
    // Alerts launch /usr/bin/say and /usr/bin/afplay from the log parse thread
    // during combat, so this has to neither block nor throw.
    //
    // Both pipes are redirected to keep the child's output out of the terminal.
    // Redirecting without reading is the part that bites: the child blocks on
    // write once the buffer fills, so it never exits, the Exited handler never
    // runs and the handle is never released. Measured before the fix, a child
    // writing 200KB with both pipes redirected and unread did not exit within
    // four seconds, while the same child unredirected exited immediately.
    [TestClass]
    public class ProcessLauncherTests
    {
        private string workingDirectory;

        [TestInitialize]
        public void Setup()
        {
            workingDirectory = Path.Combine(Path.GetTempPath(), "pigparse-launch-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(workingDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(workingDirectory))
                    Directory.Delete(workingDirectory, recursive: true);
            }
            catch
            {
            }
        }

        private static bool WaitForFile(string path, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path))
                    return true;

                Thread.Sleep(50);
            }

            return false;
        }

        [TestMethod]
        public void Start_ChildProducingMoreThanThePipeBuffer_StillRunsToCompletion()
        {
            // Arrange
            // The sentinel is touched only after all the output has been written,
            // so its absence means the child was still blocked on a full pipe.
            var sentinel = Path.Combine(workingDirectory, "finished");
            var script =
                "for i in $(seq 1 4000); do " +
                "echo 0123456789012345678901234567890123456789012345678901234567890123456789; " +
                "done; touch " + sentinel;

            // Act
            new ProcessLauncher().Start("/bin/sh", new[] { "-c", script });

            // Assert
            // A bounded wait, so a regression fails here rather than hanging the
            // whole run.
            Assert.IsTrue(
                WaitForFile(sentinel, TimeSpan.FromSeconds(15)),
                "The child never finished writing, which means nothing was draining its output.");
        }

        [TestMethod]
        public void Start_ChildWritingToStandardError_StillRunsToCompletion()
        {
            // Arrange
            // afplay writes to stderr when handed a file it cannot play, so the
            // error pipe needs draining for the same reason as the output one.
            var sentinel = Path.Combine(workingDirectory, "finished-stderr");
            var script =
                "for i in $(seq 1 4000); do " +
                "echo 0123456789012345678901234567890123456789012345678901234567890123456789 1>&2; " +
                "done; touch " + sentinel;

            // Act
            new ProcessLauncher().Start("/bin/sh", new[] { "-c", script });

            // Assert
            Assert.IsTrue(
                WaitForFile(sentinel, TimeSpan.FromSeconds(15)),
                "The child never finished writing to stderr, which means that pipe was not drained.");
        }

        [TestMethod]
        public void Start_ExecutableThatDoesNotExist_DoesNotThrow()
        {
            // Arrange
            // Reached from the log parse thread, so a launch failure has to stay
            // contained rather than stopping the parser.
            var missing = Path.Combine(workingDirectory, "no-such-binary");

            // Act
            new ProcessLauncher().Start(missing, Array.Empty<string>());

            // Assert
            Assert.IsFalse(File.Exists(missing));
        }

        [TestMethod]
        public void Start_ShortLivedChild_ReleasesItsProcessHandle()
        {
            // Arrange
            // A raid fires hundreds of alerts. Each Process holds an OS handle
            // until disposed, and the Exited handler is what releases it.
            var before = Process.GetCurrentProcess().HandleCount;

            // Act
            for (var i = 0; i < 25; i++)
                new ProcessLauncher().Start("/usr/bin/true", Array.Empty<string>());

            Thread.Sleep(2000);

            // Assert
            // Generous, since the runtime moves handles around for its own
            // reasons; the point is that twenty five launches do not leave
            // twenty five handles behind.
            var after = Process.GetCurrentProcess().HandleCount;
            Assert.IsTrue(
                after - before < 25,
                $"Handle count went from {before} to {after} across 25 launches, so they are not being released.");
        }
    }
}
