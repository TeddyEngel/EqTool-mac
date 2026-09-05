using EQTool.Core.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EQTool.Core.Tests
{
    // Triggers let a user write their own pattern, and without a bound on match
    // time a pattern that backtracks catastrophically does not fail, it runs
    // forever. Matching happens on the log parsing thread, so the client stops
    // updating and stays stopped, and restarting does not help because the same
    // log line is waiting to be matched again.
    //
    // Upstream sets the bound in the static constructor of its WPF App class,
    // which is not part of this build, so nothing set it here.
    //
    // What these can and cannot show is worth being clear about. Regex reads the
    // process default once, the first time the type is used anywhere, and caches
    // it forever. By the time a test runs, something in the host has almost
    // certainly built a Regex already, so a test cannot install the default and
    // then observe it taking effect. What it can do is check the value is put in
    // place, and that the value chosen is actually short enough to abort the
    // pathological case rather than sit there.
    [TestClass]
    public class RegexSafetyTests
    {
        [TestMethod]
        public void Install_PutsTheTimeoutInPlace()
        {
            // Act
            RegexSafety.Install();

            // Assert
            Assert.AreEqual(
                TimeSpan.FromMilliseconds(RegexSafety.DefaultMatchTimeoutMilliseconds),
                RegexSafety.Configured);
        }

        [TestMethod]
        public void Install_LeavesAnExistingValueAlone()
        {
            // Arrange
            // Whatever ran first wins, since Regex has already cached whatever it
            // saw. Overwriting would only make the recorded value disagree with
            // the one actually in force.
            RegexSafety.Install();
            var first = RegexSafety.Configured;

            // Act
            RegexSafety.Install();

            // Assert
            Assert.AreEqual(first, RegexSafety.Configured);
        }

        [TestMethod]
        public void TheChosenTimeout_AbortsACatastrophicPattern()
        {
            // Arrange
            // The classic exponential backtrack. Unbounded, this does not return
            // in any useful amount of time.
            var pattern = new Regex(
                "^(a+)+$",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(RegexSafety.DefaultMatchTimeoutMilliseconds));
            var input = new string('a', 40) + "!";

            // Act
            // On a worker with a hard cap, so that a missing bound fails this test
            // rather than hanging the run.
            var work = Task.Run(() =>
            {
                try
                {
                    _ = pattern.IsMatch(input);
                    return "matched";
                }
                catch (RegexMatchTimeoutException)
                {
                    return "aborted";
                }
            });

            var returned = work.Wait(TimeSpan.FromSeconds(10));

            // Assert
            Assert.IsTrue(returned, "The match never came back, so nothing was bounding it.");
            Assert.AreEqual("aborted", work.Result);
        }

        [TestMethod]
        public void TheChosenTimeout_LeavesOrdinaryPatternsAlone()
        {
            // Arrange
            // 25ms has to be generous enough for the patterns the client actually
            // runs, which are ordinary line matches against a log entry.
            var pattern = new Regex(
                @"^\[.*\] (?<who>[\w` ]+) begins to cast a spell\.$",
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(RegexSafety.DefaultMatchTimeoutMilliseconds));

            // Act
            var matched = pattern.IsMatch("[Mon Sep 01 12:00:00 2025] Pigy begins to cast a spell.");

            // Assert
            Assert.IsTrue(matched);
        }
    }
}
