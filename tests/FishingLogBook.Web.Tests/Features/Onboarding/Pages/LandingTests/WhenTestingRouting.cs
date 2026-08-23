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
        cut.Find("#landing-brand-logo").GetAttribute("src")
            .Should().Be("images/brand/brand-horizontal-transparent.png");
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
    public void ItShouldNotShowTheProbeActionWithoutProvisionedMetadata()
    {
        // Arrange
        using var context = CreateContext(Onboarding(false), isAuthenticated: false);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.FindAll("#landing-webauthn-probe-action").Should().BeEmpty();
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("webauthn-capability-probe");
    }

    [Fact]
    public async Task ItShouldShowTheProvisionedProbeWithoutNavigatingUntilTapped()
    {
        // Arrange
        var probe = Probe(hasMetadata: true);
        await using var context = CreateContext(Onboarding(false), isAuthenticated: false, probe);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.Find("#landing-webauthn-probe-action").TextContent.Should().Contain("Test offline device unlock");
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("webauthn-capability-probe");
        await probe.Received(1).HasMetadataAsync(Arg.Any<CancellationToken>());

        // Act
        await cut.Find("#landing-webauthn-probe-action").ClickAsync();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith("/diagnostics/webauthn-capability-probe");
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
