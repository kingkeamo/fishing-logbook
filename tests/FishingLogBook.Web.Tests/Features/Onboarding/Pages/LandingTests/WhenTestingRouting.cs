using System.Security.Claims;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
        cut.Find("#offline-diagnostics-button").GetAttribute("href").Should().Be("/offline-diagnostics");
        cut.Markup.Should().NotContain("Authorizing...");
    }

    [Fact]
    public async Task ItShouldRenderOfflineDiagnosticsWithoutWaitingForAuthenticationOrOfflineUnlock()
    {
        // Arrange
        var authentication = new TaskCompletionSource<AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var authenticationStateProvider = Authentication(authentication.Task);
        await using var context = CreateContext(
            Onboarding(false),
            authenticationStateProvider: authenticationStateProvider);
        AddApplicationShell(context);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/offline-diagnostics");

        // Act
        var cut = context.Render<App>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-diagnostics-page").Should().NotBeNull());
        cut.Markup.Should().NotContain("Authorizing...");
        context.Services.GetRequiredService<IOfflineOwnerContextService>().IsUnlocked.Should().BeFalse();
        await authenticationStateProvider.DidNotReceive().GetAuthenticationStateAsync();
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
    public async Task ItShouldOfferOfflineUnlockWithoutWaitingForAuthentication()
    {
        // Arrange
        var authentication = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var device = OfflineAccessDevice(hasReadyEntitlement: true);
        var network = Network(isOnline: false);
        await using var context = CreateContext(
            Onboarding(false),
            authenticationStateProvider: Authentication(authentication.Task),
            offlineAccessDevice: device,
            network: network);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#landing-open-offline").Should().NotBeNull());
        cut.FindAll("#landing-offline-not-configured").Should().BeEmpty();
        await device.Received(1).HasReadyEntitlementAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().UnlockAsync(Arg.Any<CancellationToken>());
        await network.Received(1).StartMonitoringAsync(Arg.Any<CancellationToken>());
        await network.Received(1).IsOnlineAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowOfflineAvailabilityFailureWhenNoEntitlementExists()
    {
        // Arrange
        var device = OfflineAccessDevice(hasReadyEntitlement: false);
        await using var context = CreateContext(Onboarding(false), isAuthenticated: false, offlineAccessDevice: device);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#landing-offline-availability-failed").Should().BeEmpty());
        cut.FindAll("#landing-open-offline").Should().BeEmpty();
        cut.FindAll("#landing-offline-not-configured").Should().BeEmpty();
        await device.Received(1).HasReadyEntitlementAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().UnlockAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExplainUnconfiguredOfflineAccessOnlyWhenOffline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var device = OfflineAccessDevice(hasReadyEntitlement: false);
        var network = Network(isOnline: false);
        var onboarding = Onboarding(false);
        await using var context = CreateContext(
            onboarding,
            isAuthenticated: false,
            offlineAccessDevice: device,
            network: network);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#landing-offline-not-configured").TextContent
                .Should().Contain("Offline access isn't set up on this device"));
        cut.FindAll("#landing-open-offline").Should().BeEmpty();
        cut.FindAll("#landing-offline-availability-failed").Should().BeEmpty();
        await network.Received(1).StartMonitoringAsync(Arg.Any<CancellationToken>());
        await network.Received(1).IsOnlineAsync(Arg.Any<CancellationToken>());
        await device.Received(1).HasReadyEntitlementAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().UnlockAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().SetupAsync(
            Arg.Any<OfflineAccessIdentityModel>(),
            Arg.Any<CancellationToken>());
        await device.DidNotReceive().RemoveAsync(
            Arg.Any<OfflineAccessIdentityModel>(),
            Arg.Any<CancellationToken>());
        await onboarding.DidNotReceive().IsCompletedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogConnectivityDiscoveryFailureWithoutShowingOfflineSetupGuidance()
    {
        // Arrange
        var failure = new JSException("network-state-unavailable");
        var network = Substitute.For<INetworkService>();
        network.StartMonitoringAsync(Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            logging: logging,
            network: network);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#landing-offline-not-configured").Should().BeEmpty());
        await logging.Received(1).LogErrorAsync(
            "landing connectivity",
            Arg.Is<Exception>(exception => ReferenceEquals(exception, failure)),
            CancellationToken.None);
        await network.Received(1).StartMonitoringAsync(Arg.Any<CancellationToken>());
        await network.DidNotReceive().IsOnlineAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowGuidanceWhenConnectivityChangesToOffline()
    {
        // Arrange
        var device = OfflineAccessDevice(hasReadyEntitlement: false);
        var network = Network(isOnline: true);
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            offlineAccessDevice: device,
            network: network);
        var cut = context.Render<LandingPage>();
        cut.WaitForAssertion(() => cut.FindAll("#landing-offline-not-configured").Should().BeEmpty());

        // Act
        network.ConnectivityChanged += Raise.Event<Action<bool>>(false);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#landing-offline-not-configured").Should().NotBeNull());
        await device.Received(1).HasReadyEntitlementAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().UnlockAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHideGuidanceWhenConnectivityChangesToOnline()
    {
        // Arrange
        var device = OfflineAccessDevice(hasReadyEntitlement: false);
        var network = Network(isOnline: false);
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            offlineAccessDevice: device,
            network: network);
        var cut = context.Render<LandingPage>();
        cut.WaitForAssertion(() => cut.Find("#landing-offline-not-configured").Should().NotBeNull());

        // Act
        network.ConnectivityChanged += Raise.Event<Action<bool>>(true);

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#landing-offline-not-configured").Should().BeEmpty());
        await device.Received(1).HasReadyEntitlementAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().UnlockAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogAndRecoverWhenOfflineAvailabilityDiscoveryFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var failure = new JSException("indexeddb-read:UnknownError");
        var device = Substitute.For<IOfflineAccessDeviceService>();
        device.HasReadyEntitlementAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => throw failure,
                _ => new OfflineAccessAvailabilityModel("ready", "ready-record-found"));
        var logging = Substitute.For<ILoggingService>();
        var network = Network(isOnline: false);
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            logging: logging,
            offlineAccessDevice: device,
            network: network);
        var cut = context.Render<LandingPage>();
        cut.WaitForAssertion(() =>
            cut.Find("#landing-offline-availability-failed").TextContent
                .Should().Contain("couldn't check offline access"));
        cut.FindAll("#landing-offline-not-configured").Should().BeEmpty();

        // Act
        await cut.Find("#landing-offline-availability-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#landing-open-offline").Should().NotBeNull());
        cut.FindAll("#landing-offline-availability-failed").Should().BeEmpty();
        await device.Received(2).HasReadyEntitlementAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().UnlockAsync(Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "landing offline availability",
            Arg.Is<Exception>(exception => ReferenceEquals(exception, failure)),
            CancellationToken.None);
        await network.Received(1).IsOnlineAsync(Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/");
    }

    [Fact]
    public async Task ItShouldShowFrenchOfflineSetupGuidance()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var device = OfflineAccessDevice(hasReadyEntitlement: false);
        var network = Network(isOnline: false);
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            offlineAccessDevice: device,
            network: network);

        // Act
        var cut = context.Render<LandingPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#landing-offline-not-configured").TextContent
                .Should().Contain("L'accès hors ligne n'est pas configuré"));
        await device.Received(1).HasReadyEntitlementAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().UnlockAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAnUnexpectedOfflineAvailabilityStateAsARetryableFailure()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var device = Substitute.For<IOfflineAccessDeviceService>();
        device.HasReadyEntitlementAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => new OfflineAccessAvailabilityModel("future-state", "ignored-detail"),
                _ => new OfflineAccessAvailabilityModel("ready", "ready-record-found"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(
            Onboarding(false),
            isAuthenticated: false,
            logging: logging,
            offlineAccessDevice: device);
        var cut = context.Render<LandingPage>();
        cut.WaitForAssertion(() => cut.Find("#landing-offline-availability-failed").Should().NotBeNull());

        // Act
        await cut.Find("#landing-offline-availability-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#landing-open-offline").Should().NotBeNull());
        cut.FindAll("#landing-offline-availability-failed").Should().BeEmpty();
        await device.Received(2).HasReadyEntitlementAsync(Arg.Any<CancellationToken>());
        await device.DidNotReceive().UnlockAsync(Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "landing offline availability",
            Arg.Is<OfflineAccessDiscoveryException>(exception => exception.Message == "unexpected-state"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldUnlockOnlyAfterTheExplicitAction()
    {
        // Arrange
        var device = OfflineAccessDevice(hasReadyEntitlement: true);
        device.UnlockAsync(Arg.Any<CancellationToken>()).Returns(
            new OfflineAccessUnlockResultModel("unlocked", Guid.Parse("11111111-1111-1111-1111-111111111111"), 1));
        await using var context = CreateContext(Onboarding(false), isAuthenticated: false, offlineAccessDevice: device);
        var cut = context.Render<LandingPage>();
        cut.WaitForAssertion(() => cut.Find("#landing-open-offline").Should().NotBeNull());

        // Act
        await cut.Find("#landing-open-offline").ClickAsync();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/offline/catches");
        context.Services.GetRequiredService<IOfflineOwnerContextService>().Owner?.EntitlementVersion.Should().Be(1);
        await device.Received(1).UnlockAsync(Arg.Any<CancellationToken>());
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
    public async Task ItShouldRedirectWhenAuthenticationChangesAfterLandingInitiallyLoadsAnonymous()
    {
        // Arrange
        var authentication = new MutableAuthenticationStateProvider(Anonymous());
        var onboarding = Onboarding(true);
        await using var context = CreateContext(
            onboarding,
            isAuthenticated: false,
            authenticationStateProvider: authentication);
        var cut = context.Render<LandingPage>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.Uri.Should().EndWith("/");

        // Act
        authentication.SetAuthenticationState(Authenticated());

        // Assert
        cut.WaitForAssertion(() => navigation.Uri.Should().EndWith("/catches"));
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

    private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private sealed class MutableAuthenticationStateProvider(AuthenticationState authenticationState)
        : AuthenticationStateProvider
    {
        private AuthenticationState _authenticationState = authenticationState;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(_authenticationState);

        public void SetAuthenticationState(AuthenticationState authenticationState)
        {
            _authenticationState = authenticationState;
            NotifyAuthenticationStateChanged(Task.FromResult(authenticationState));
        }
    }
}
