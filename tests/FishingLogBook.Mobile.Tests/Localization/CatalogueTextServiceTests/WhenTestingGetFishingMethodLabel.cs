using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Localization.CatalogueTextServiceTests;

public class WhenTestingGetFishingMethodLabel : BaseCatalogueTextServiceTest
{
    [Theory]
    [InlineData("", "Fly")]
    [InlineData("Ice", "Ice fishing")]
    public void ItShouldFallBackToTheServerNameWhenTheCodeIsNotTranslated(string code, string canonicalName)
    {
        // Arrange
        // Act
        var label = Sut.GetFishingMethodLabel(code, canonicalName);
        // Assert
        label.Should().Be(canonicalName);
    }

    [Theory]
    [InlineData("Fly", "Pêche à la mouche")]
    [InlineData("Spinning", "Pêche au lancer")]
    [InlineData("Bait", "Pêche à l’appât")]
    [InlineData("Lure", "Pêche aux leurres")]
    [InlineData("Trolling", "Pêche à la traîne")]
    public void ItShouldTranslateEverySeededMethodIntoFrench(string code, string expected)
    {
        // Arrange
        Text.SetCulture("fr-FR");
        // Act
        var label = Sut.GetFishingMethodLabel(code, code);
        // Assert
        label.Should().Be(expected);
    }

    [Theory]
    [InlineData("Fly", "Fly")]
    [InlineData("Spinning", "Spinning")]
    [InlineData("Bait", "Bait")]
    [InlineData("Lure", "Lure")]
    [InlineData("Trolling", "Trolling")]
    public void ItShouldUseTheEnglishLabelForEverySeededMethod(string code, string expected)
    {
        // Arrange
        // Act
        var label = Sut.GetFishingMethodLabel(code, "server name");
        // Assert
        label.Should().Be(expected);
    }
}
