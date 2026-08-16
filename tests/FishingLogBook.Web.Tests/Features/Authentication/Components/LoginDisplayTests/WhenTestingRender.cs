using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Authentication.Components.LoginDisplay;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.LoginDisplayTests;

public class WhenTestingRender : BaseLoginDisplayTest
{
    [Fact]
    public async Task ItShouldShowSignIn_WhenUnauthenticated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: false);

        // Act
        var cut = context.Render<LoginDisplay>();

        // Assert
        cut.Find("#auth-sign-in-button").TextContent.Should().Contain("Sign in");
        cut.Find("#auth-create-account-button").TextContent.Should().Contain("Create account");
        cut.FindAll("#auth-sign-out-button").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowFrenchSignIn_WhenUnauthenticated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext(isAuthenticated: false);

        // Act
        var cut = context.Render<LoginDisplay>();

        // Assert
        cut.Find("#auth-sign-in-button").TextContent.Should().Contain("Connexion");
        cut.Find("#auth-create-account-button").TextContent.Should().Contain("Créer un compte");
    }

    [Fact]
    public async Task ItShouldShowSignOut_WhenAuthenticated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);

        // Act
        var cut = context.Render<LoginDisplay>();

        // Assert
        cut.Find("#auth-sign-out-button").TextContent.Should().Contain("Sign out");
        cut.FindAll("#auth-sign-in-button").Should().BeEmpty();
        cut.FindAll("#auth-create-account-button").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNavigateToLogin_WhenSignInIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: false);
        var cut = context.Render<LoginDisplay>();

        // Act
        cut.Find("#auth-sign-in-button").Click();
        var uri = context.Services.GetRequiredService<NavigationManager>().Uri;

        // Assert
        uri.Should().Contain("authentication/login");
    }

    [Fact]
    public async Task ItShouldNavigateToLogout_WhenSignOutIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(isAuthenticated: true);
        var cut = context.Render<LoginDisplay>();

        // Act
        cut.Find("#auth-sign-out-button").Click();
        var uri = context.Services.GetRequiredService<NavigationManager>().Uri;

        // Assert
        uri.Should().Contain("authentication/logout");
    }
}
