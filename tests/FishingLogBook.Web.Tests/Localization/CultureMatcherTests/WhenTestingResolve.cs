using AwesomeAssertions;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Localization.CultureMatcherTests;

public class WhenTestingResolve : BaseCultureMatcherTest
{
    [Theory]
    [InlineData("fr", null, CultureNames.French)]
    [InlineData("fr-FR", null, CultureNames.French)]
    [InlineData("en-GB", null, CultureNames.English)]
    [InlineData("en-US", null, CultureNames.English)]
    [InlineData(null, "fr-FR", CultureNames.French)]
    [InlineData(null, "en-IE", CultureNames.English)]
    [InlineData(null, "de-DE", CultureNames.English)]
    [InlineData("", "", CultureNames.English)]
    [InlineData("en-GB", "fr-FR", CultureNames.English)]
    public void ItShouldSelectSupportedCulture_WhenStoredOrBrowserLanguageIsProvided(
        string? stored,
        string? browser,
        string expected)
    {
        // Arrange
        // Act
        var culture = CultureMatcher.Resolve(stored, browser);

        // Assert
        culture.Should().Be(expected);
    }
}
