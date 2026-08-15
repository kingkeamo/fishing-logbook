using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Components;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.LanguageSwitcherTests;

public class WhenTestingRender : BaseLanguageSwitcherTest
{
    [Fact]
    public async Task ItShouldShowLanguageControl()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var cultureService = Substitute.For<ICultureService>();
        await using var context = CreateContext(cultureService);

        // Act
        var cut = context.Render<LanguageSwitcher>();

        // Assert
        cut.Find("#language-menu-button").GetAttribute("aria-label").Should().Be("Language");
        await cultureService.DidNotReceive().SetCultureAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLanguageControl_WhenUiCultureIsFrench()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var cultureService = Substitute.For<ICultureService>();
        await using var context = CreateContext(cultureService);

        // Act
        var cut = context.Render<LanguageSwitcher>();

        // Assert
        cut.Find("#language-menu-button").GetAttribute("aria-label").Should().Be("Langue");
    }
}
