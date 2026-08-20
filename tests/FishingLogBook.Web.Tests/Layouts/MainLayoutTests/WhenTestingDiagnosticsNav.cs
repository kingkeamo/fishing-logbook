using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Layouts.MainLayout;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class WhenTestingDiagnosticsNav : BaseMainLayoutTest
{
    [Fact]
    public async Task ItShouldShowTheDiagnosticsButton()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.Find("#diagnostics-nav-button").TextContent.Should().Contain("Diagnostics");
        cut.Find("#diagnostics-nav-button").GetAttribute("href").Should().Be("/diagnostics");
    }

    [Fact]
    public async Task ItShouldShowFrenchDiagnosticsButton()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.Find("#diagnostics-nav-button").TextContent.Should().Contain("Diagnostics");
        cut.Find("#diagnostics-nav-button").GetAttribute("href").Should().Be("/diagnostics");
    }
}
