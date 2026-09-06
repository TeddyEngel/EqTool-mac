using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EQTool.Avalonia.Tests
{
    // A window only records its geometry and its open state because it called
    // WindowPreferences.Attach. Nothing else writes either one.
    //
    // App.ReopenLastSession brings back the windows that were open at exit, and
    // ShouldReopen needs a stored rect to tell "was open" from "never seen". So a
    // window listed there that never attaches can never come back, and its entry
    // is dead. That happened: the settings window was on the reopen list and was
    // the only view that did not attach, so the line could not fire and the window
    // did not remember its position either.
    //
    // This reads the sources rather than exercising the windows. Constructing
    // MapWindow, DpsWindow, ConsoleWindow, MobInfoWindow or SettingsWindow through
    // the path the client uses calls AppServices.Initialize, which opens the real
    // settings file, so the behavioural version of this test would read a live
    // configuration. The invariant is structural anyway: the call is either
    // written down or it is not.
    [TestClass]
    public class WindowAttachCoverageTests
    {
        private static string ViewsDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "native", "EQTool.Avalonia", "Views");
                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            return null;
        }

        private static string AppSource()
        {
            var views = ViewsDirectory();
            return views == null ? null : Path.Combine(Directory.GetParent(views).FullName, "App.axaml.cs");
        }

        [TestMethod]
        public void EveryWindowOnTheReopenListAlsoAttaches()
        {
            // Arrange
            var views = ViewsDirectory();
            Assert.IsNotNull(views, "Could not locate the Views directory from the test output.");

            var app = File.ReadAllText(AppSource());

            // Each Reopen call names the state and the WindowManager entry point.
            var reopened = Regex.Matches(app, @"Reopen\(settings\.(\w+), WindowManager\.Show(\w+)")
                .Select(m => m.Groups[2].Value)
                .ToList();

            Assert.AreNotEqual(0, reopened.Count, "No Reopen calls were found, so this test is checking nothing.");

            // Act
            // WindowManager.ShowMap opens MapWindow, ShowDps opens DpsWindow, and
            // so on, with Show{Name} matching {Name}Window apart from the two
            // spelled differently.
            var fileFor = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Map"] = "MapWindow",
                ["Dps"] = "DpsWindow",
                ["MobInfo"] = "MobInfoWindow",
                ["Console"] = "ConsoleWindow",
                ["Overlay"] = "EventOverlayWindow",
                ["Settings"] = "SettingsWindow",
            };

            var missing = new System.Collections.Generic.List<string>();
            foreach (var entry in reopened)
            {
                Assert.IsTrue(fileFor.ContainsKey(entry), $"Reopen names Show{entry}, which this test does not know how to map to a view.");

                var path = Path.Combine(views, fileFor[entry] + ".axaml.cs");
                Assert.IsTrue(File.Exists(path), $"{path} does not exist.");

                if (!File.ReadAllText(path).Contains("WindowPreferences.Attach"))
                    missing.Add(fileFor[entry]);
            }

            // Assert
            Assert.AreEqual(
                0,
                missing.Count,
                "These are on the reopen list and never call Attach, so nothing records their state and they can never come back: "
                    + string.Join(", ", missing));
        }

        [TestMethod]
        public void EveryWindowThatAttachesUsesItsOwnState()
        {
            // Arrange
            // Two windows sharing one WindowState would overwrite each other's
            // position and fight over the closed flag.
            var views = ViewsDirectory();
            Assert.IsNotNull(views);

            var used = new System.Collections.Generic.List<string>();

            // Act
            foreach (var file in Directory.GetFiles(views, "*.axaml.cs"))
            {
                var text = File.ReadAllText(file);
                if (!text.Contains("WindowPreferences.Attach"))
                    continue;

                foreach (Match match in Regex.Matches(text, @"Attach\(this,\s*[\w\.]*?(\w+WindowState)"))
                    used.Add(match.Groups[1].Value);
            }

            // Assert
            Assert.AreNotEqual(0, used.Count, "No Attach calls were found, so this test is checking nothing.");
            CollectionAssert.AllItemsAreUnique(used, "Two windows are attached to the same WindowState: " + string.Join(", ", used));
        }
    }
}
