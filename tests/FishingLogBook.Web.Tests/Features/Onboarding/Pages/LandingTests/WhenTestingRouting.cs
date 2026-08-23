using System.Security.Claims;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using LandingPage = FishingLogBook.Web.Features.Onboarding.Pages.Landing.Landing;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.LandingTests;

public class WhenTestingRouting : BaseLandingTest
{
    [Fact]
    public async Task ItShouldRenderLandingThroughTheRouterWhileAuthenticationIsPending()
    {
        // Arrange
        var authentication = new TaskCompletionSource<AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var context = CreateContext(
            Onboarding(false),
            authenticationStateProvider: Authentication(authentication.Task));
        AddApplicationShell(context);

        // Act
        var cut = context.Render<App>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#public-landing-page").Should().NotBeNull());
        cut.Markup.Should().NotContain("Authorizing...");
    }

    [Fact]
    public void ItShouldKeepApplicationPagesProtected()
    {
        // Arrange
        Type[] protectedPages =
        [
            typeof(FishingLogBook.Web.Features.Catch.Pages.CatchList.CatchList),
            typeof(FishingLogBook.Web.Features.Catch.Pages.RecordCatch.RecordCatch),
            typeof(FishingLogBook.Web.Features.Catch.Pages.CatchEdit.CatchEdit),
            typeof(FishingLogBook.Web.Features.Profile.Pages.Profile.Profile),
            typeof(FishingLogBook.Web.Features.Onboarding.Pages.Onboarding.Onboarding)
        ];

        // Act
        var attributes = protectedPages.ToDictionary(
            page => page,
            page => page.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        // Assert
        attributes.Should().OnlyContain(entry => entry.Value.Length == 1);
    }

    [Fact]
    public async Task ItShouldRenderThePublicFrontDoorWhileAuthenticationIsPending()
    {
        // Arrange
        var authentication = new TaskCompletionSource<AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var onboarding = Onboarding(false);
        await using var context = CreateContext(
            onboarding,
            authenticationStateProvider: Authentication(authentication.Task));

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.Find("#public-landing-page").Should().NotBeNull();
        cut.FindAll("#landing-loading").Should().BeEmpty();
        await onboarding.DidNotReceive().IsCompletedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheProbeAvailableWhileAuthenticationIsPending()
    {
        // Arrange
        var authentication = new TaskCompletionSource<AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = Probe(hasMetadata: true);
        await using var context = CreateContext(
            Onboarding(false),
            probe: probe,
            authenticationStateProvider: Authentication(authentication.Task));

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#landing-webauthn-probe-action").Should().NotBeNull());
        await probe.Received(1).HasMetadataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepThePublicFrontDoorWhenAuthenticationFails()
    {
        // Arrange
        var exception = new InvalidOperationException("OIDC unavailable");
        var logging = Substitute.For<ILoggingService>();
        var onboarding = Onboarding(false);
        await using var context = CreateContext(
            onboarding,
            authenticationStateProvider: Authentication(Task.FromException<AuthenticationState>(exception)),
            logging: logging);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.Find("#public-landing-page").Should().NotBeNull();
        await logging.Received(1).LogErrorAsync(
            "landing authentication resolution",
            exception,
            CancellationToken.None);
        await onboarding.DidNotReceive().IsCompletedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRedirectAfterPendingAuthenticationResolvesForACompletedUser()
    {
        // Arrange
        var authentication = new TaskCompletionSource<AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var onboarding = Onboarding(true);
        await using var context = CreateContext(
            onboarding,
            authenticationStateProvider: Authentication(authentication.Task));
        context.Render<LandingPage>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var navigated = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnLocationChanged(object? sender, LocationChangedEventArgs args)
        {
            if (args.Location.EndsWith("/catches", StringComparison.Ordinal))
            {
                navigated.TrySetResult();
            }
        }

        navigation.LocationChanged += OnLocationChanged;

        // Act
        try
        {
            authentication.SetResult(Authenticated());
            await navigated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            navigation.LocationChanged -= OnLocationChanged;
        }

        // Assert
        navigation.Uri.Should().EndWith("/catches");
        await onboarding.Received(1).IsCompletedAsync(Arg.Any<CancellationToken>());
    }

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

    private static AuthenticationState Authenticated()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "angler@example.test")],
            "Test");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
