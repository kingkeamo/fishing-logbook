using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using NSubstitute;
using TripListPage = FishingLogBook.Web.Features.Trips.Pages.TripList.TripList;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripListTests;

public class WhenTestingRetry : BaseTripListTest
{
    [Fact]
    public async Task ItShouldReloadBothSourcesWhenTheAnglerRetriesAfterAFailedLoad()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var attempts = 0;
        var store = Substitute.For<ITripStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => attempts == 0
                ? Task.FromException<IReadOnlyList<TripModel>>(new InvalidOperationException("IndexedDB unavailable."))
                : Task.FromResult<IReadOnlyList<TripModel>>([LocalTrip()]));
        var client = Substitute.For<ITripClient>();
        client.GetMyAsync(Arg.Any<CancellationToken>())
            .Returns(_ => attempts == 0
                ? Task.FromException<IReadOnlyList<TripSummaryDto>>(new HttpRequestException("offline"))
                : Task.FromResult<IReadOnlyList<TripSummaryDto>>([]));
        await using var context = CreateContext(store, client);
        var cut = context.Render<TripListPage>();
        cut.WaitForAssertion(() => cut.Find("#trip-list-retry").Should().NotBeNull());
        attempts++;

        // Act
        await cut.Find("#trip-list-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-list-item-{LocalTripId:D}").Should().NotBeNull());
        cut.FindAll("#trip-list-load-failed").Should().BeEmpty();
        await store.Received(2).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await client.Received(2).GetMyAsync(Arg.Any<CancellationToken>());
    }
}
