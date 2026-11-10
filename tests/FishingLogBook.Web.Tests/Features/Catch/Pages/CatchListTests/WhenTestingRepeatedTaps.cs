using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingRepeatedTaps : BaseCatchListTest
{
    [Fact]
    public async Task ItShouldStartOnlyOneTripWhenStartFishingIsTappedTwiceBeforeTheFirstFinishes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(Array.Empty<CatchModel>());
        var release = new TaskCompletionSource<TripModel>();
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        activeTrip.StartAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(release.Task);
        await using var context = CreateContext(store);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        var first = cut.Find("#trip-start-link").ClickAsync();
        var second = cut.Find("#trip-start-link").ClickAsync();
        release.SetResult(new TripModel(Guid.NewGuid(), OwnerUserId, TripConstants.Active, DateTimeOffset.UtcNow));
        await first;
        await second;

        // Assert
        await activeTrip.Received(1).StartAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryACatchOnlyOnceWhenTheRetryIsTappedTwiceBeforeTheFirstFinishes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), SyncStatus.FailedToSynchronise)]);
        var release = new TaskCompletionSource();
        var synchroniser = Substitute.For<ILogbookSynchroniser>();
        synchroniser.RetryAsync(catchId, Arg.Any<CancellationToken>()).Returns(release.Task);
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find($"#catch-sync-retry-{catchId:D}").Should().NotBeNull());

        // Act
        var first = cut.Find($"#catch-sync-retry-{catchId:D}").ClickAsync();
        var second = cut.Find($"#catch-sync-retry-{catchId:D}").ClickAsync();
        release.SetResult();
        await first;
        await second;

        // Assert
        await synchroniser.Received(1).RetryAsync(catchId, Arg.Any<CancellationToken>());
    }
}
