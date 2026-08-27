using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Components.ActiveTripBanner;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.ActiveTripBannerTests;

public class WhenTestingRender
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    [Fact]
    public async Task ItShouldRenderNothingWhenNoTripIsActive()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService(null);
        await using var context = CreateContext(activeTrip);

        // Act
        var cut = context.Render<ActiveTripBanner>();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#active-trip-banner").Should().BeEmpty());
        await activeTrip.Received(1).GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderNothingWhenTheOwnerCannotBeResolved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("The current user is not signed in."));
        var activeTrip = ActiveTripService(ActiveTrip());
        var logging = QuietLogging();
        await using var context = CreateContext(activeTrip, owner, logging);

        // Act
        var cut = context.Render<ActiveTripBanner>();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#active-trip-banner").Should().BeEmpty());
        await activeTrip.DidNotReceive().GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "resolving the active trip banner",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAnnounceTheTripAndLinkToIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService(ActiveTrip());
        await using var context = CreateContext(activeTrip);

        // Act
        var cut = context.Render<ActiveTripBanner>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-banner").TextContent.Should().Contain("Fishing trip in progress"));
        cut.Find("#active-trip-banner").GetAttribute("role").Should().Be("status");
        cut.Find("#active-trip-banner-update").GetAttribute("href")
            .Should().Be($"/trips/{TripId:D}");
        cut.Find("#active-trip-banner-update").TextContent.Should().Contain("Update trip");
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var activeTrip = ActiveTripService(ActiveTrip());
        await using var context = CreateContext(activeTrip);

        // Act
        var cut = context.Render<ActiveTripBanner>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-banner").TextContent.Should().Contain("Sortie de pêche en cours"));
        cut.Find("#active-trip-banner-update").TextContent.Should().Contain("Modifier la sortie");
    }

    [Fact]
    public async Task ItShouldDisappearWhenTheTripIsFinished()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = Substitute.For<IActiveTripService>();
        var finished = false;
        activeTrip.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<TripModel?>(finished ? null : ActiveTrip()));
        await using var context = CreateContext(activeTrip);
        var cut = context.Render<ActiveTripBanner>();
        cut.WaitForAssertion(() => cut.Find("#active-trip-banner").Should().NotBeNull());

        // Act
        finished = true;
        activeTrip.StateChanged += Raise.Event<EventHandler>(activeTrip, EventArgs.Empty);

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#active-trip-banner").Should().BeEmpty());
        await activeTrip.Received(2).GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    private static BunitContext CreateContext(
        IActiveTripService activeTrip,
        ILocalCatchOwnerService? owner = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(activeTrip);
        context.Services.AddSingleton(owner ?? SignedInOwner());
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    private static IActiveTripService ActiveTripService(TripModel? trip)
    {
        var service = Substitute.For<IActiveTripService>();
        service.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(trip);
        return service;
    }

    private static ILocalCatchOwnerService SignedInOwner()
    {
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        return owner;
    }

    private static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    private static TripModel ActiveTrip()
    {
        return new TripModel(TripId, OwnerUserId, TripConstants.Active, StartedOn);
    }
}
