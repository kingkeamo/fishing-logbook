using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Dependencies;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripPhotographStoreTests;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripStoreTests;

namespace FishingLogBook.Web.Tests.Common.Offline.Dependencies.TripDependencyServiceTests;

public class BaseTripDependencyServiceTest
{
    protected static readonly Guid OwnerUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid SecondTripId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid CatchId =
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly DateTimeOffset StartedOn =
        DateTimeOffset.Parse("2026-08-17T09:00:00Z");

    protected readonly MemoryTripStore TripStore = new();
    protected readonly MemoryTripPhotographStore TripPhotographStore = new();
    protected readonly MemoryTripNoteStore TripNoteStore = new();
    protected readonly MemoryCatchStore CatchStore = new();
    protected readonly TripDependencyService Sut;

    protected BaseTripDependencyServiceTest()
    {
        Sut = new TripDependencyService(TripStore, TripPhotographStore, TripNoteStore, CatchStore);
    }

    protected async Task GivenTripAsync(
        Guid tripId,
        SyncStatus syncStatus,
        Guid? ownerUserId = null)
    {
        await TripStore.SaveAsync(
            new TripModel(
                tripId,
                ownerUserId ?? OwnerUserId,
                TripConstants.Active,
                StartedOn,
                SyncStatus: syncStatus),
            CancellationToken.None);
    }

    protected async Task GivenTripPhotographAsync(
        Guid photographId,
        Guid tripId,
        SyncStatus syncStatus,
        Guid? ownerUserId = null)
    {
        await TripPhotographStore.SaveAsync(
            new TripPhotographModel(
                photographId,
                tripId,
                ownerUserId ?? OwnerUserId,
                "image/jpeg",
                StartedOn.AddMinutes(30),
                Bytes: [1, 2, 3],
                SyncStatus: syncStatus),
            CancellationToken.None);
    }

    protected async Task GivenTripNoteAsync(
        Guid noteId,
        Guid tripId,
        SyncStatus syncStatus,
        Guid? ownerUserId = null)
    {
        await TripNoteStore.SaveAsync(
            new TripNoteModel(
                noteId,
                tripId,
                ownerUserId ?? OwnerUserId,
                "water dropped a foot",
                StartedOn.AddMinutes(45),
                syncStatus),
            CancellationToken.None);
    }

    protected async Task GivenCatchAsync(
        Guid catchId,
        Guid? tripId,
        SyncStatus syncStatus,
        SyncStatus? metadataSyncStatus = null,
        Guid? ownerUserId = null)
    {
        var owner = ownerUserId ?? OwnerUserId;
        await CatchStore.SaveAsync(
            new CatchModel(
                catchId,
                StartedOn.AddHours(1),
                [new CatchPhotographModel(Guid.NewGuid(), catchId, "image/jpeg", [1, 2, 3])],
                CaughtByUserId: owner,
                SyncStatus: syncStatus,
                MetadataSyncStatus: metadataSyncStatus ?? syncStatus,
                RecordedByUserId: owner,
                TripId: tripId),
            CancellationToken.None);
    }
}
