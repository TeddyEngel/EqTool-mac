using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace EQTool.Avalonia.Tests
{
    // Establishes whether Avalonia can run a real app loop in this test process
    // without a display server. If it can, windows can be instantiated and their
    // visual trees inspected, which is otherwise only possible by screenshotting a
    // running app on an awake screen.
    // The app's own BuildAvaloniaApp uses UsePlatformDetect, which would pick the
    // native macOS backend and need a display. This one selects the headless
    // backend instead, and is what HeadlessUnitTestSession is pointed at.
    public static class HeadlessTestApp
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
        }
    }

    [TestClass]
    public class HeadlessProbeTests
    {
        private static HeadlessUnitTestSession session;

        [ClassInitialize]
        public static void StartSession(TestContext context)
        {
            session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp));
        }

        [ClassCleanup]
        public static void StopSession()
        {
            session?.Dispose();
        }

        [TestMethod]
        public void HeadlessSession_CanCreateAndShowAWindow()
        {
            var succeeded = false;

            session.Dispatch(() =>
            {
                var window = new Window { Width = 200, Height = 100 };
                window.Show();
                succeeded = window.IsVisible;
                window.Close();
            }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(succeeded, "Headless session could not show a window.");
        }

        [TestMethod]
        public void HeadlessSession_BuildsAVisualTreeFromContent()
        {
            string readBack = null;

            session.Dispatch(() =>
            {
                var text = new TextBlock { Text = "rendered headlessly" };
                var window = new Window { Content = text };
                window.Show();

                readBack = (window.Content as TextBlock)?.Text;
                window.Close();
            }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("rendered headlessly", readBack);
        }
    }
}
