using System.Security.Claims;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Authentication.Components.LoginDisplay;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.LoginDisplayTests;

public class WhenTestingCurrentUserEmail : BaseLoginDisplayTest
{
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
}
