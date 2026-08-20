using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using LandingPage = FishingLogBook.Web.Features.Onboarding.Pages.Landing.Landing;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.LandingTests;

public class WhenTestingRouting : BaseLandingTest
{
    [Fact]
    public async Task ItShouldRenderThePublicFrontDoorWithoutLoadingAProfile()
    {
        // Arrange
        using var culture = TestCulture.Use(FishingLogBook.Web.Localization.CultureNames.English);
        var onboarding = Onboarding(false);
        await using var context = CreateContext(onboarding, isAuthenticated: false);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.Find("#public-landing-page").TextContent.Should().Contain("Your private fishing logbook");
        cut.Find("#landing-create-account").TextContent.Should().Contain("Create account");
        cut.Find("#landing-sign-in").TextContent.Should().Contain("Sign in");
        await onboarding.DidNotReceive().IsCompletedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHandCreateAccountToTheExistingAuthenticationFlow()
    {
        // Arrange
        var onboarding = Onboarding(false);
        await using var context = CreateContext(onboarding, isAuthenticated: false);
        var cut = context.Render<LandingPage>();

        // Act
        await cut.Find("#landing-create-account").ClickAsync();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Contain("authentication/login");
        await onboarding.DidNotReceive().IsCompletedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHandSignInToTheExistingAuthenticationFlow()
    {
        // Arrange
        var onboarding = Onboarding(false);
        await using var context = CreateContext(onboarding, isAuthenticated: false);
        var cut = context.Render<LandingPage>();

        // Act
        await cut.Find("#landing-sign-in").ClickAsync();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Contain("authentication/login");
        await onboarding.DidNotReceive().IsCompletedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRouteAnIncompleteUserToOnboarding()
    {
        // Arrange
        var onboarding = Onboarding(false);
        await using var context = CreateContext(onboarding);

        // Act
        context.Render<LandingPage>();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/onboarding");
        await onboarding.Received(1).IsCompletedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRouteACompletedUserToTheLogbook()
    {
        // Arrange
        var onboarding = Onboarding(true);
        await using var context = CreateContext(onboarding);

        // Act
        context.Render<LandingPage>();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/catches");
        await onboarding.Received(1).IsCompletedAsync(Arg.Any<CancellationToken>());
    }
}
