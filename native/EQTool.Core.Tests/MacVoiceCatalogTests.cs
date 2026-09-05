using EQTool.Core.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace EQTool.Core.Tests
{
    [TestClass]
    public class MacVoiceCatalogTests
    {
        [TestMethod]
        public void Parse_StandardListing_ReadsNameAndLocale()
        {
            // Arrange
            var listing = "Albert              en_US    # Hello! My name is Albert.";

            // Act
            var voices = MacVoiceCatalog.Parse(listing);

            // Assert
            Assert.AreEqual(1, voices.Count);
            Assert.AreEqual("Albert", voices[0].Name);
            Assert.AreEqual("en_US", voices[0].Locale);
        }

        [TestMethod]
        public void Parse_NameWithAccentedCharacters_IsPreserved()
        {
            // Arrange
            var listing = "Amélie              fr_CA    # Bonjour! Je m’appelle Amélie.";

            // Act
            var voices = MacVoiceCatalog.Parse(listing);

            // Assert
            Assert.AreEqual("Amélie", voices[0].Name);
            Assert.AreEqual("fr_CA", voices[0].Locale);
        }

        [TestMethod]
        public void Parse_NameContainingSpaces_IsNotTruncated()
        {
            // Arrange
            // Some voices are multi-word, so splitting on whitespace and taking the
            // first token would silently drop half the name.
            var listing = "Eddy (French (France)) fr_FR    # Bonjour!";

            // Act
            var voices = MacVoiceCatalog.Parse(listing);

            // Assert
            Assert.AreEqual("Eddy (French (France))", voices[0].Name);
            Assert.AreEqual("fr_FR", voices[0].Locale);
        }

        [TestMethod]
        public void Parse_CommentContainingLocaleLikeText_DoesNotConfuseTheParser()
        {
            // Arrange
            var listing = "Daniel              en_GB    # Hello, my locale is en_US in this sentence.";

            // Act
            var voices = MacVoiceCatalog.Parse(listing);

            // Assert
            Assert.AreEqual("Daniel", voices[0].Name);
            Assert.AreEqual("en_GB", voices[0].Locale);
        }

        [TestMethod]
        public void Parse_MultipleLines_ReturnsEveryVoice()
        {
            // Arrange
            var listing = string.Join("\n", new[]
            {
                "Albert              en_US    # Hello! My name is Albert.",
                "Alice               it_IT    # Ciao! Mi chiamo Alice.",
                "Alva                sv_SE    # Hej! Jag heter Alva."
            });

            // Act
            var voices = MacVoiceCatalog.Parse(listing);

            // Assert
            Assert.AreEqual(3, voices.Count);
            CollectionAssert.AreEqual(
                new[] { "Albert", "Alice", "Alva" },
                voices.Select(a => a.Name).ToArray());
        }

        [TestMethod]
        public void Parse_EmptyListing_ReturnsNoVoices()
        {
            // Act
            var voices = MacVoiceCatalog.Parse("   ");

            // Assert
            Assert.AreEqual(0, voices.Count);
        }

        [TestMethod]
        public void Available_OnThisMachine_ReturnsVoicesWithEnglishFirst()
        {
            // Act
            var voices = MacVoiceCatalog.Available();

            // Assert
            Assert.IsTrue(voices.Count > 0, "say -v ? returned no voices.");
            Assert.IsTrue(
                voices[0].Locale.StartsWith("en", System.StringComparison.OrdinalIgnoreCase),
                "Expected an English voice first, got " + voices[0].Locale);
        }
    }
}
