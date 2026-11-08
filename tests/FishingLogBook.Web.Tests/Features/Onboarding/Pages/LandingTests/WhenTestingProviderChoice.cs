using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using LandingPage = FishingLogBook.Web.Features.Onboarding.Pages.Landing.Landing;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.LandingTests;

public class WhenTestingProviderChoice : BaseLandingTest
{
    [Fact]
    public async Task ItShouldKeepTheCreateAccountAndSignInButtonsWhenNoProvidersAreConfigured()
    {
        // Arrange
        await using var context = CreateContext(Onboarding(false), isAuthenticated: false);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.Find("#landing-create-account").Should().NotBeNull();
        cut.Find("#landing-sign-in").Should().NotBeNull();
        cut.FindAll("#sign-in-chooser").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheProviderChoicesOnTheLandingPageInsteadOfRedirecting()
    {
        // Arrange
        await using var context = CreateContext(Onboarding(false), isAuthenticated: false, socialProviders: ["google"]);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.Find("#sign-in-google").TextContent.Trim().Should().Be("Continue with Google");
        cut.Find("#sign-in-email").TextContent.Trim().Should().Be("Continue with email");
        cut.FindAll("#landing-create-account").Should().BeEmpty();
        cut.FindAll("#landing-sign-in").Should().BeEmpty();
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("sign-in");
    }

    [Fact]
    public async Task ItShouldHideTheProviderChoicesWhileOffline()
    {
        // Arrange
        await using var context = CreateContext(Onboarding(false), isAuthenticated: false, network: Network(isOnline: false), socialProviders: ["google"]);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.FindAll("#sign-in-google").Should().BeEmpty();
        cut.FindAll("#sign-in-email").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldSignInWithTheChosenProviderStraightFromTheLandingPage()
    {
        // Arrange
        await using var context = CreateContext(Onboarding(false), isAuthenticated: false, socialProviders: ["google"]);
        var cut = context.Render<LandingPage>();

        // Act
        await cut.Find("#sign-in-google").ClickAsync();

        // Assert
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        navigation.Uri.Should().Contain("authentication/login");
        navigation.History.First().Options.HistoryEntryState.Should().Contain("identity_provider").And.Contain("Google");
    }

    [Fact]
    public async Task ItShouldSignInWithEmailWithoutPreselectingAProvider()
    {
        // Arrange
        await using var context = CreateContext(Onboarding(false), isAuthenticated: false, socialProviders: ["google"]);
        var cut = context.Render<LandingPage>();

        // Act
        await cut.Find("#sign-in-email").ClickAsync();

        // Assert
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        navigation.Uri.Should().Contain("authentication/login");
        navigation.History.First().Options.HistoryEntryState.Should().NotContain("identity_provider");
    }
}
