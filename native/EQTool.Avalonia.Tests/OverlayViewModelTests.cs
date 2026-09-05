using Avalonia.Media;
using EQTool.Avalonia.Services;
using EQTool.Avalonia.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;

namespace EQTool.Avalonia.Tests
{
    // The overlay window itself cannot be rendered in a test, but its countdown
    // arithmetic and text formatting are ordinary logic and are the parts most
    // likely to be quietly wrong.
    [TestClass]
    public class OverlayTimerBarViewModelTests
    {
        [TestMethod]
        public void CountdownText_UnderAMinute_ShowsWholeSeconds()
        {
            // Arrange
            var bar = new OverlayTimerBarViewModel("Dragon Roar", 36, null);

            // Assert
            StringAssert.EndsWith(bar.CountdownText, "s");
            Assert.IsFalse(bar.CountdownText.Contains("m"));
        }

        [TestMethod]
        public void CountdownText_OverAMinute_ShowsMinutesAndSeconds()
        {
            // Arrange
            var bar = new OverlayTimerBarViewModel("Sebilis Pull", 150, null);

            // Assert
            StringAssert.Contains(bar.CountdownText, "m");
            StringAssert.EndsWith(bar.CountdownText, "s");
        }

        [TestMethod]
        public void CountdownText_ExactlySixtySeconds_CrossesIntoMinutes()
        {
            // Arrange
            // 60 is the boundary the formatter branches on, so it is worth pinning
            // rather than trusting the comparison direction.
            var bar = new OverlayTimerBarViewModel("Boundary", 60, null);

            // Assert
            StringAssert.Contains(bar.CountdownText, "m");
        }

        [TestMethod]
        public void PercentLeft_AtCreation_IsEffectivelyFull()
        {
            // Arrange
            var bar = new OverlayTimerBarViewModel("Fresh", 60, null);

            // Assert
            Assert.IsTrue(bar.PercentLeft > 99.0, "Expected a nearly full bar, got " + bar.PercentLeft);
        }

        [TestMethod]
        public void PercentLeft_NeverExceedsItsBounds()
        {
            // Arrange
            var bar = new OverlayTimerBarViewModel("Bounded", 1, null);

            // Act
            Thread.Sleep(1200);
            bar.Tick();

            // Assert
            Assert.IsTrue(bar.PercentLeft >= 0, "Percent went negative: " + bar.PercentLeft);
            Assert.IsTrue(bar.PercentLeft <= 100, "Percent exceeded 100: " + bar.PercentLeft);
        }

        [TestMethod]
        public void HasExpired_AfterItsDuration_ReportsTrue()
        {
            // Arrange
            var bar = new OverlayTimerBarViewModel("Short", 1, null);
            Assert.IsFalse(bar.HasExpired);

            // Act
            Thread.Sleep(1200);

            // Assert
            Assert.IsTrue(bar.HasExpired);
        }

        [TestMethod]
        public void SecondsLeft_NeverGoesNegative()
        {
            // Arrange
            // The overlay removes bars on a 250ms tick, so a bar can outlive its
            // duration by a fraction of a second before it is pruned. That must
            // read as zero rather than as a negative countdown.
            var bar = new OverlayTimerBarViewModel("Short", 1, null);

            // Act
            Thread.Sleep(1200);

            // Assert
            Assert.AreEqual(0, bar.SecondsLeft);
        }

        [TestMethod]
        public void ZeroDuration_IsTreatedAsOneSecondRatherThanDividingByZero()
        {
            // Arrange
            // A trigger configured with no duration would otherwise divide by zero
            // when computing PercentLeft.
            var bar = new OverlayTimerBarViewModel("Zero", 0, null);

            // Assert
            Assert.IsFalse(double.IsNaN(bar.PercentLeft));
            Assert.IsFalse(double.IsInfinity(bar.PercentLeft));
        }

        [TestMethod]
        public void Name_IsCarriedThrough()
        {
            // Arrange
            var bar = new OverlayTimerBarViewModel("Ring 8", 30, null);

            // Assert
            Assert.AreEqual("Ring 8", bar.Name);
        }
    }

    [TestClass]
    public class OverlayBannerViewModelTests
    {
        [TestMethod]
        public void Banner_CarriesTextAndExpiry()
        {
            // Arrange
            var expiry = DateTime.Now.AddSeconds(5);

            // Act
            var banner = new OverlayBannerViewModel("Enrage!", null, expiry);

            // Assert
            Assert.AreEqual("Enrage!", banner.Text);
            Assert.AreEqual(expiry, banner.ExpiresAt);
        }
    }

    [TestClass]
    public class ShimBrushMapTests
    {
        [TestMethod]
        public void Resolve_SolidColourBrush_PreservesTheExactArgb()
        {
            // Arrange
            // Trigger colours travel as the shim's SolidColorBrush; losing the
            // bytes here would silently repaint every trigger.
            var shim = new WpfSolidColorBrush(WpfColor.FromArgb(200, 255, 90, 40));

            // Act
            var resolved = ShimBrushMap.Resolve(shim) as ISolidColorBrush;

            // Assert
            Assert.IsNotNull(resolved);
            Assert.AreEqual(200, resolved.Color.A);
            Assert.AreEqual(255, resolved.Color.R);
            Assert.AreEqual(90, resolved.Color.G);
            Assert.AreEqual(40, resolved.Color.B);
        }

        [TestMethod]
        public void Resolve_SameBrushTwice_ReturnsTheCachedInstance()
        {
            // Arrange
            var shim = new WpfSolidColorBrush(WpfColor.FromRgb(10, 20, 30));

            // Act
            var first = ShimBrushMap.Resolve(shim);
            var second = ShimBrushMap.Resolve(shim);

            // Assert
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void Resolve_Null_FallsBackRatherThanThrowing()
        {
            // Act
            // With no Application running there is no theme to fall back to, so
            // this returns null. The point is that it does not throw: a trigger
            // with no colour must not take the overlay down.
            var resolved = ShimBrushMap.Resolve(null);

            // Assert
            Assert.IsNull(resolved);
        }
    }
}
