using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingStartFishing : BaseCatchListTest
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    [Fact]
    public async Task ItShouldOfferStartFishingWhenNoTripIsActive()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = EmptyStore();
        var activeTrip = ActiveTripService(null);
        await using var context = CreateContext(store);
        context.Services.AddSingleton(activeTrip);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-start-link").TextContent.Should().Contain("Start fishing"));
        cut.FindAll("#trip-update-link").Should().BeEmpty();
        cut.Find("#catch-record-link").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldOfferContinueWhenATripIsActive()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = EmptyStore();
        var activeTrip = ActiveTripService(ActiveTrip());
        await using var context = CreateContext(store);
        context.Services.AddSingleton(activeTrip);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-update-link").TextContent.Should().Contain("Update trip"));
        cut.FindAll("#trip-start-link").Should().BeEmpty();
        cut.Find("#trip-update-link").GetAttribute("href").Should().Be($"/trips/{TripId:D}");
    }

    [Fact]
    public async Task ItShouldStartATripAndNavigateToIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = EmptyStore();
        var activeTrip = ActiveTripService(null);
        activeTrip.StartAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(ActiveTrip());
        await using var context = CreateContext(store);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

        // Assert
        await activeTrip.Received(1).StartAsync(OwnerUserId, Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith($"/trips/{TripId:D}");
    }

    [Fact]
    public async Task ItShouldRecoverToTheExistingTripWhenAnotherIsAlreadyActive()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = EmptyStore();
        var activeTrip = Substitute.For<IActiveTripService>();
        var started = false;
        activeTrip.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<TripModel?>(started ? ActiveTrip() : null));
        activeTrip.StartAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<Task<TripModel>>(_ =>
            {
                started = true;
                throw new TripAlreadyActiveException();
            });
        await using var context = CreateContext(store);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith($"/trips/{TripId:D}");
        cut.Markup.Should().NotContain("TripAlreadyActiveException");
    }

    [Fact]
    public async Task ItShouldStillRenderTheLogbookWhenTheActiveTripLookupFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = EmptyStore();
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("active-read timed out."));
        var logging = QuietLogging();
        await using var context = CreateContext(store, logging: logging);
        context.Services.AddSingleton(activeTrip);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());
        cut.FindAll("#catch-list-load-failed").Should().BeEmpty();
        await logging.Received(1).LogErrorAsync(
            "resolving the active trip",
            Arg.Any<TimeoutException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = EmptyStore();
        var activeTrip = ActiveTripService(null);
        await using var context = CreateContext(store);
        context.Services.AddSingleton(activeTrip);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-start-link").TextContent.Should().Contain("Commencer la pêche"));
    }

    private static ICatchStore EmptyStore()
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Web.Features.Catch.Models.CatchModel>());
        return store;
    }

    private static IActiveTripService ActiveTripService(TripModel? trip)
    {
        var service = Substitute.For<IActiveTripService>();
        service.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(trip);
        return service;
    }

    private static TripModel ActiveTrip()
    {
        return new TripModel(TripId, OwnerUserId, TripConstants.Active, StartedOn);
    }
}
