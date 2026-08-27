using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Dependencies;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripStoreTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripSynchroniserTests;

public class BaseTripSynchroniserTest
{
    protected static readonly Guid OwnerUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid SecondTripId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly DateTimeOffset StartedOn =
        DateTimeOffset.Parse("2026-08-17T09:00:00Z");

    protected readonly ITripDependencyService MockTripDependency =
        Substitute.For<ITripDependencyService>();
    protected readonly ITripClient MockTripClient = Substitute.For<ITripClient>();
    protected readonly INetworkService MockNetworkService = Substitute.For<INetworkService>();
    protected readonly IActiveTripService MockActiveTripService =
        Substitute.For<IActiveTripService>();
    protected readonly IDiagnosticLogger MockDiagnostics = Substitute.For<IDiagnosticLogger>();
    protected readonly ILoggingService MockLogging = Substitute.For<ILoggingService>();

    protected BaseTripSynchroniserTest()
    {
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<TripDto>(0));
    }

    protected TripSynchroniser CreateSut(MemoryTripStore store)
    {
        return new TripSynchroniser(
            store,
            MockTripDependency,
            MockTripClient,
            MockNetworkService,
            MockActiveTripService,
            MockDiagnostics,
            MockLogging);
    }

    protected static TripModel CreateTrip(
        Guid? tripId = null,
        Guid? ownerUserId = null,
        string status = TripConstants.Active,
        DateTimeOffset? startedOn = null,
        DateTimeOffset? endedOn = null,
        SyncStatus syncStatus = SyncStatus.SavedLocally,
        DateTimeOffset? syncedAt = null,
        string? title = null,
        string? placeName = null,
        TripLocationModel? location = null)
    {
        return new TripModel(
            tripId ?? TripId,
            ownerUserId ?? OwnerUserId,
            status,
            startedOn ?? StartedOn,
            endedOn,
            title,
            placeName,
            location,
            syncStatus,
            syncedAt);
    }

    protected static TripLocationModel CreateLocation()
    {
        return new TripLocationModel(
            53.2707,
            -9.0568,
            7,
            DateTimeOffset.Parse("2026-08-17T08:59:00Z"),
            "DeviceGps",
            "Private",
            "1");
    }

    protected static async Task<MemoryTripStore> CreateStoreAsync(params TripModel[] trips)
    {
        var store = new MemoryTripStore();
        foreach (var trip in trips)
        {
            await store.SaveAsync(trip, CancellationToken.None);
        }

        return store;
    }
}
