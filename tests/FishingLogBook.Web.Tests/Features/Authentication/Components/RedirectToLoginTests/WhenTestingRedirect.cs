using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.Authentication.Components.RedirectToLogin;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.RedirectToLoginTests;

public class WhenTestingRedirect : BaseRedirectToLoginTest
{
    [Fact]
    public async Task ItShouldNavigateStraightToLoginWhenNoSocialProvidersAreConfigured()
    {
        // Arrange
        await using var context = CreateContext([]);

        // Act
        var cut = context.Render<RedirectToLogin>();
        var uri = context.Services.GetRequiredService<NavigationManager>().Uri;

        // Assert
        uri.Should().Contain("authentication/login");
        cut.FindAll("#sign-in-chooser").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOfferAppleGoogleAndEmailWithoutNavigatingUntilOneIsChosen()
    {
        // Arrange
        await using var context = CreateContext(["google", "apple"]);

        // Act
        var cut = context.Render<RedirectToLogin>();

        // Assert
        cut.Find("#sign-in-apple").TextContent.Trim().Should().Be("Continue with Apple");
        cut.Find("#sign-in-google").TextContent.Trim().Should().Be("Continue with Google");
        cut.Find("#sign-in-email").TextContent.Trim().Should().Be("Continue with email");
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("authentication/login");
    }

    [Fact]
    public async Task ItShouldOnlyOfferConfiguredProviders()
    {
        // Arrange
        await using var context = CreateContext(["google", "facebook"]);

        // Act
        var cut = context.Render<RedirectToLogin>();

        // Assert
        cut.FindAll("#sign-in-google").Should().HaveCount(1);
        cut.FindAll("#sign-in-apple").Should().BeEmpty();
        cut.FindAll("#sign-in-email").Should().HaveCount(1);
    }

    [Theory]
    [InlineData("#sign-in-google", "Google")]
    [InlineData("#sign-in-apple", "SignInWithApple")]
    public async Task ItShouldRequestTheChosenProviderFromCognito(string selector, string expectedProvider)
    {
        // Arrange
        await using var context = CreateContext(["google", "apple"]);
        var cut = context.Render<RedirectToLogin>();

        // Act
        await cut.Find(selector).ClickAsync();

        // Assert
        var navigation = context.Services.GetRequiredService<BunitNavigationManager>();
        navigation.Uri.Should().Contain("authentication/login");
        navigation.History.First().Options.HistoryEntryState.Should().Contain("identity_provider").And.Contain(expectedProvider);
    }

    [Fact]
    public async Task ItShouldKeepEmailPasswordSignInWithoutPreselectingAProvider()
    {
        // Arrange
        await using var context = CreateContext(["google", "apple"]);
        var cut = context.Render<RedirectToLogin>();

        // Act
        await cut.Find("#sign-in-email").ClickAsync();

        // Assert
        var navigation = context.Services.GetRequiredService<BunitNavigationManager>();
        navigation.Uri.Should().Contain("authentication/login");
        navigation.History.First().Options.HistoryEntryState.Should().NotContain("identity_provider");
    }
}

