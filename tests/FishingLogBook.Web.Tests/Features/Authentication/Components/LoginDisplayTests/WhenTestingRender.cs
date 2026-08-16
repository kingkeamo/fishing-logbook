using System.Security.Claims;
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
    public async Task ItShouldShowSignInWhenUnauthenticated()
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
    public async Task ItShouldShowFrenchSignInWhenUnauthenticated()
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
    public async Task ItShouldShowSignOutWhenAuthenticated()
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
        cut.FindAll("#auth-current-user-email").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepSignOutWhenEmailIsMissing()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(
            isAuthenticated: true,
            new Claim("sub", "cognito-sub-abc"),
            new Claim("preferred_username", "eamonn123"),
            new Claim("name", "Eamonn Connolly"));

        // Act
        var cut = context.Render<LoginDisplay>();

        // Assert
        cut.Find("#auth-sign-out-button").TextContent.Should().Contain("Sign out");
        cut.FindAll("#auth-current-user-email").Should().BeEmpty();
        cut.Markup.Should().NotContain("cognito-sub-abc");
        cut.Markup.Should().NotContain("eamonn123");
        cut.Markup.Should().NotContain("Eamonn Connolly");
        cut.FindAll("#auth-sign-in-button").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheAuthenticatedEmail()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(
            isAuthenticated: true,
            new Claim("name", "Eamonn Connolly"),
            new Claim("email", "e.connolly10@gmail.com"),
            new Claim("sub", "cognito-sub-abc"));

        // Act
        var cut = context.Render<LoginDisplay>();

        // Assert
        cut.Find("#auth-current-user-email").TextContent.Should().Contain("e.connolly10@gmail.com");
        cut.Find("#auth-sign-out-button").TextContent.Should().Contain("Sign out");
        cut.Markup.Should().NotContain("cognito-sub-abc");
        cut.Markup.Should().NotContain("Eamonn Connolly");
        cut.Markup.Should().NotContain(Guid.Empty.ToString());
    }

    [Fact]
    public async Task ItShouldNavigateToLoginWhenSignInIsClicked()
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
    public async Task ItShouldNavigateToLogoutWhenSignOutIsClicked()
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
