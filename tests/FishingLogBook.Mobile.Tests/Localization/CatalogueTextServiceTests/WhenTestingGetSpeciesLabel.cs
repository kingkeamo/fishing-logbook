using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Localization.CatalogueTextServiceTests;

public class WhenTestingGetSpeciesLabel : BaseCatalogueTextServiceTest
{
    [Fact]
    public void ItShouldFallBackToTheServerNameWhenTheCodeIsNotTranslated()
    {
        // Arrange
        Text.SetCulture("fr-FR");
        // Act
        var label = Sut.GetSpeciesLabel("Zander", "Zander");
        // Assert
        label.Should().Be("Zander");
    }

    [Theory]
    [InlineData("BrownTrout", "Truite brune")]
    [InlineData("RainbowTrout", "Truite arc-en-ciel")]
    [InlineData("BrookTrout", "Omble de fontaine")]
    [InlineData("SeaTrout", "Truite de mer")]
    [InlineData("Salmon", "Saumon")]
    [InlineData("Pike", "Brochet")]
    [InlineData("Perch", "Perche")]
    [InlineData("Carp", "Carpe")]
    [InlineData("Bream", "Brème")]
    [InlineData("Roach", "Gardon")]
    [InlineData("Tench", "Tanche")]
    [InlineData("Grayling", "Ombre commun")]
    public void ItShouldTranslateEverySeededSpeciesIntoFrench(string code, string expected)
    {
        // Arrange
        Text.SetCulture("fr-FR");
        // Act
        var label = Sut.GetSpeciesLabel(code, code);
        // Assert
        label.Should().Be(expected);
    }

    [Theory]
    [InlineData("BrownTrout", "Brown Trout")]
    [InlineData("Salmon", "Salmon")]
    [InlineData("Grayling", "Grayling")]
    public void ItShouldUseTheEnglishLabelFromTheSharedCatalogueResources(string code, string expected)
    {
        // Arrange
        // Act
        var label = Sut.GetSpeciesLabel(code, "server name");
        // Assert
        label.Should().Be(expected);
    }
}
