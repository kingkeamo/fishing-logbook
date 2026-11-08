using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Authentication.Components.UserMenu;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.UserMenuTests;

public class WhenTestingSignIn : BaseUserMenuTest
{
    [Theory]
    [InlineData("#auth-sign-in-button")]
    [InlineData("#auth-create-account-button")]
    public async Task ItShouldGoStraightToCognitoWhenNoProvidersAreConfigured(string selector)
    {
        // Arrange
        await using var context = CreateContext(ProfileSummary(), isAuthenticated: false);
        var cut = context.Render<UserMenu>();

        // Act
        await cut.Find(selector).ClickAsync();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Contain("authentication/login");
    }

    [Theory]
    [InlineData("#auth-sign-in-button")]
    [InlineData("#auth-create-account-button")]
    public async Task ItShouldOpenTheProviderChooserWhenProvidersAreConfigured(string selector)
    {
        // Arrange
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary, isAuthenticated: false, socialProviders: ["google"]);
        var cut = context.Render<UserMenu>();

        // Act
        await cut.Find(selector).ClickAsync();

        // Assert
        var uri = context.Services.GetRequiredService<NavigationManager>().Uri;
        uri.Should().Contain("/sign-in?returnUrl=");
        uri.Should().NotContain("authentication/login");
        await profileSummary.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }
}
