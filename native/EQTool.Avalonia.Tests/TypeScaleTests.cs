using EQTool.Avalonia.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace EQTool.Avalonia.Tests
{
    // The font size setting had no effect for the whole life of the client. It
    // works by rescaling the type tokens, so these pin the arithmetic: the
    // ordering the design file sets out has to survive, and the default has to
    // reproduce the file rather than approximately resemble it.
    [TestClass]
    public class TypeScaleTests
    {
        [TestMethod]
        public void Compute_AtTheDefault_ReproducesTheDesignFile()
        {
            // Arrange
            // Leaving the slider alone must change nothing at all, or every
            // window shifts slightly the first time the setting is applied.
            var expected = TypeScale.DefaultTokens;

            // Act
            var actual = TypeScale.Compute(TypeScale.BaseFontSize);

            // Assert
            foreach (var pair in expected)
                Assert.AreEqual(pair.Value, actual[pair.Key], 0.001, pair.Key);
        }

        [TestMethod]
        public void Compute_AtDoubleTheBase_DoublesEveryToken()
        {
            // Act
            var actual = TypeScale.Compute(TypeScale.BaseFontSize * 2);

            // Assert
            foreach (var pair in TypeScale.DefaultTokens)
                Assert.AreEqual(pair.Value * 2, actual[pair.Key], 0.001, pair.Key);
        }

        [TestMethod]
        public void Compute_KeepsTheOrderingAtEverySizeTheSliderOffers()
        {
            // Arrange
            // The scale is deliberate: the countdown is the only thing allowed to
            // be large, and the group label sits at the bottom. A rounding rule
            // that collapsed two steps together would lose that.
            var order = new[] { "TypeMicro", "TypeCaption", "TypeBody", "TypeCountdown", "TypeHeading", "TypeTitle" };

            for (var fontSize = 9; fontSize <= 24; fontSize++)
            {
                // Act
                var tokens = TypeScale.Compute(fontSize);

                // Assert
                for (var i = 1; i < order.Length; i++)
                {
                    Assert.IsTrue(
                        tokens[order[i]] >= tokens[order[i - 1]],
                        $"At font size {fontSize}, {order[i]} ({tokens[order[i]]}) fell below {order[i - 1]} ({tokens[order[i - 1]]}).");
                }
            }
        }

        [TestMethod]
        public void Compute_NeverProducesAnUnreadableSize()
        {
            // Arrange
            // TypeMicro is the smallest token, and the slider bottoms out at 9.
            for (var fontSize = 9; fontSize <= 24; fontSize++)
            {
                // Act
                var smallest = TypeScale.Compute(fontSize).Values.Min();

                // Assert
                Assert.IsTrue(smallest >= 6, $"At font size {fontSize} the smallest token was {smallest}.");
            }
        }

        [TestMethod]
        public void ScaleToken_RoundsToAHalfPoint()
        {
            // Arrange
            // 9 * 9 / 12 is 6.75, which would render blurrier than a rounded size.

            // Act
            var scaled = TypeScale.ScaleToken(9, 9);

            // Assert
            Assert.AreEqual(7.0, scaled, 0.001);
        }

        [TestMethod]
        public void ScaleToken_IsProportional()
        {
            // Assert
            // Two tokens keep their ratio, which is what makes this a rescale of
            // the existing design rather than a new one.
            var micro = TypeScale.ScaleToken(9, 18);
            var title = TypeScale.ScaleToken(20, 18);

            Assert.AreEqual(13.5, micro, 0.001);
            Assert.AreEqual(30.0, title, 0.001);
        }
    }
}
