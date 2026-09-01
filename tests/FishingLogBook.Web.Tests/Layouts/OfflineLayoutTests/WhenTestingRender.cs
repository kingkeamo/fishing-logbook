using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Layouts.OfflineLayout;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.OfflineLayoutTests;

public class WhenTestingRender : BaseOfflineLayoutTest
{
    [Fact]
    public async Task ItShouldUseTheSharedBrandingAndOnlyOfflineNavigation()
    {
        // Arrange
        await using var context = CreateContext(out _);

        // Act
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(layout => layout.Body, builder => builder.AddContent(0, "Offline content")));

        // Assert
        cut.Find("#app-brand-mark").Should().NotBeNull();
        cut.Find("#language-menu-button").Should().NotBeNull();
        cut.Find("#theme-toggle-button").Should().NotBeNull();
        cut.Find("#offline-diagnostics-button").GetAttribute("href").Should().Be("/offline-diagnostics");
        cut.Find("#offline-catches-nav-link").Should().NotBeNull();
        cut.Find("#offline-record-nav-link").Should().NotBeNull();
        cut.Find("#offline-lock-nav-link").Should().NotBeNull();
        cut.FindAll("#offline-trips-nav-link").Should().BeEmpty();
        cut.FindAll("#profile-nav-link").Should().BeEmpty();
        cut.FindAll("#app-update-banner").Should().BeEmpty();
        cut.FindAll("#user-menu-button").Should().BeEmpty();
        cut.FindAll("#auth-sign-in-button").Should().BeEmpty();
        cut.FindAll("#auth-create-account-button").Should().BeEmpty();
        cut.FindAll("#app-menu-button").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldLockOnlyTheInMemoryOfflineContextAndReturnToLanding()
    {
        // Arrange
        await using var context = CreateContext(out var owner);
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(_ => { })));

        // Act
        await cut.Find("#offline-lock-nav-link").ClickAsync();

        // Assert
        owner.IsUnlocked.Should().BeFalse();
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Be("http://localhost/");
    }

    [Fact]
    public async Task ItShouldStartReconnectMonitoringWithoutStartingOnlineLayoutHooks()
    {
        // Arrange
        var reconnect = Substitute.For<IOfflineReconnectService>();
        await using var context = CreateContext(out _, reconnect);

        // Act
        context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));
        await Task.Yield();

        // Assert
        await reconnect.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await reconnect.DidNotReceive().AttemptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepOfflineNavigationAvailableWhenAuthenticationIsRequired()
    {
        // Arrange
        var reconnect = Substitute.For<IOfflineReconnectService>();
        reconnect.State.Returns(OfflineReconnectStateEnum.AuthenticationRequired);
        await using var context = CreateContext(out _, reconnect);

        // Act
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Assert
        cut.Find("#offline-reconnect-authentication-required").Should().NotBeNull();
        cut.Find("#offline-reconnect-sign-in").Should().NotBeNull();
        cut.Find("#offline-catches-nav-link").Should().NotBeNull();
        cut.Find("#offline-record-nav-link").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldReturnToTheOriginatingCatchesRouteAfterSignInToSync()
    {
        // Arrange
        var reconnect = Substitute.For<IOfflineReconnectService>();
        reconnect.State.Returns(OfflineReconnectStateEnum.AuthenticationRequired);
        await using var context = CreateContext(out _, reconnect);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/offline/catches");
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Act
        await cut.Find("#offline-reconnect-sign-in").ClickAsync();

        // Assert
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        navigation.Uri.Should().Contain("authentication/login");
        navigation.History.First().Options.HistoryEntryState.Should().Contain("\"returnUrl\":\"/catches\"");
        navigation.History.First().Options.HistoryEntryState.Should().NotContain("reconnect=offline");
        await reconnect.DidNotReceive().AttemptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnToTheOriginatingRecordRouteAfterSignInToSync()
    {
        // Arrange
        var reconnect = Substitute.For<IOfflineReconnectService>();
        reconnect.State.Returns(OfflineReconnectStateEnum.AuthenticationRequired);
        await using var context = CreateContext(out _, reconnect);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/offline/record");
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Act
        await cut.Find("#offline-reconnect-sign-in").ClickAsync();

        // Assert
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        navigation.History.First().Options.HistoryEntryState.Should().Contain("\"returnUrl\":\"/catches/record\"");
    }

    [Fact]
    public async Task ItShouldFallBackToCatchesWhenTheCurrentOfflineRouteIsUnrecognised()
    {
        // Arrange
        var reconnect = Substitute.For<IOfflineReconnectService>();
        reconnect.State.Returns(OfflineReconnectStateEnum.AuthenticationRequired);
        await using var context = CreateContext(out _, reconnect);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/offline/unexpected");
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Act
        await cut.Find("#offline-reconnect-sign-in").ClickAsync();

        // Assert
        var navigation = (BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>();
        navigation.History.First().Options.HistoryEntryState.Should().Contain("\"returnUrl\":\"/catches\"");
    }

    [Fact]
    public async Task ItShouldNotClaimConnectionIsRestoredWhileVerifyingIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var reconnect = Substitute.For<IOfflineReconnectService>();
        reconnect.State.Returns(OfflineReconnectStateEnum.ConnectivityRestored);
        await using var context = CreateContext(out _, reconnect);

        // Act
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Assert
        var text = cut.Find("#offline-reconnect-progress").TextContent;
        text.Should().NotContain("restored");
        text.Should().NotContain("Restored");
    }

    [Fact]
    public async Task ItShouldNotClaimToBeOnlineWhenOnlyAskingForAuthentication()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var reconnect = Substitute.For<IOfflineReconnectService>();
        reconnect.State.Returns(OfflineReconnectStateEnum.AuthenticationRequired);
        await using var context = CreateContext(out _, reconnect);

        // Act
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Assert
        var text = cut.Find("#offline-reconnect-authentication-required").TextContent;
        text.Should().NotContain("back online");
    }

    [Fact]
    public async Task ItShouldNavigateOnlyAfterTheCoordinatorReportsSafeCompletion()
    {
        // Arrange
        var reconnect = Substitute.For<IOfflineReconnectService>();
        reconnect.State.Returns(OfflineReconnectStateEnum.Synchronising);
        await using var context = CreateContext(out _, reconnect);
        context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Act
        reconnect.State.Returns(OfflineReconnectStateEnum.Synchronising);
        reconnect.StateChanged += Raise.Event<EventHandler>();
        await Task.Yield();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Be("http://localhost/");

        // Act
        reconnect.State.Returns(OfflineReconnectStateEnum.Online);
        reconnect.StateChanged += Raise.Event<EventHandler>();
        await Task.Yield();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Be("http://localhost/catches");
    }
}
