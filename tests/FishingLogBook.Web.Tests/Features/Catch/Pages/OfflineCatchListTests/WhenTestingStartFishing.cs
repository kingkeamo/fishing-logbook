using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchListTests;

public class WhenTestingStartFishing : BaseOfflineCatchListTest
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    [Fact]
    public async Task ItShouldOfferStartFishingWhenNoTripIsActive()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService(null);
        await using var context = CreateContext(EmptyCatchStore());
        context.Services.AddSingleton(activeTrip);

        // Act
        var cut = context.Render<OfflineCatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#offline-trip-start-link").TextContent.Should().Contain("Start fishing"));
        cut.FindAll("#offline-trip-update-link").Should().BeEmpty();
        await activeTrip.Received(1).GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferContinueToTheOfflineTripRouteWhenOneIsActive()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService(ActiveTrip());
        await using var context = CreateContext(EmptyCatchStore());
        context.Services.AddSingleton(activeTrip);

        // Act
        var cut = context.Render<OfflineCatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#offline-trip-update-link").GetAttribute("href")
                .Should().Be($"/offline/trips/{TripId:D}"));
        cut.FindAll("#offline-trip-start-link").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldStartATripAndNavigateToTheOfflineRoute()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService(null);
        activeTrip.StartAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(ActiveTrip());
        await using var context = CreateContext(EmptyCatchStore());
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<OfflineCatchList>();
        cut.WaitForAssertion(() => cut.Find("#offline-trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-start-link").ClickAsync();

        // Assert
        await activeTrip.Received(1).StartAsync(OwnerUserId, Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith($"/offline/trips/{TripId:D}");
    }

    private static FishingLogBook.Web.Features.Catch.Offline.Stores.ICatchStore EmptyCatchStore()
    {
        var store = Substitute.For<FishingLogBook.Web.Features.Catch.Offline.Stores.ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FishingLogBook.Web.Features.Catch.Models.CatchModel>());
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
