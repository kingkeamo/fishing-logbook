using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Layouts.MainLayout;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class WhenTestingAuthentication : BaseMainLayoutTest
{
    [Fact]
    public async Task ItShouldShowSignInWhenUnauthenticated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: false);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.Find("#auth-sign-in-button").TextContent.Should().Contain("Sign in");
        cut.FindAll("#auth-sign-out-button").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowSignOutWhenAuthenticated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.Find("#auth-sign-out-button").TextContent.Should().Contain("Sign out");
        cut.FindAll("#auth-sign-in-button").Should().BeEmpty();
    }
}
