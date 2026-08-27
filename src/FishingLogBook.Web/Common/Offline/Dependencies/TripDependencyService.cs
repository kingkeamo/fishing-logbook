using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Common.Offline.Dependencies;

public sealed class TripDependencyService : ITripDependencyService
{
    private readonly ITripStore _tripStore;
    private readonly ITripPhotographStore _tripPhotographStore;
    private readonly ICatchStore _catchStore;

    public TripDependencyService(
        ITripStore tripStore,
        ITripPhotographStore tripPhotographStore,
        ICatchStore catchStore)
    {
        _tripStore = tripStore;
        _tripPhotographStore = tripPhotographStore;
        _catchStore = catchStore;
    }

    public async Task<bool> IsTripReadyForServerAsync(
        Guid ownerUserId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty || tripId == Guid.Empty)
        {
            return false;
        }

        var trip = await _tripStore.GetAsync(ownerUserId, tripId, cancellationToken);
        if (trip is null)
        {
            return true;
        }

        return trip.SyncStatus == SyncStatus.Synchronised;
    }

    public async Task<IReadOnlyCollection<Guid>> GetTripsAwaitingDependentsAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (ownerUserId == Guid.Empty)
        {
            return [];
        }

        var catches = await _catchStore.GetMetadataAsync(ownerUserId, cancellationToken);
        var awaitingPhotographs = await _tripPhotographStore.GetTripsWithPendingPhotographsAsync(
            ownerUserId,
            cancellationToken);
        return catches
            .Where(IsAwaitingServer)
            .Select(catchRecord => catchRecord.TripId)
            .OfType<Guid>()
            .Concat(awaitingPhotographs)
            .Distinct()
            .ToArray();
    }

    private static bool IsAwaitingServer(CatchModel catchRecord)
    {
        if (catchRecord.TripId is null)
        {
            return false;
        }

        return catchRecord.SyncStatus != SyncStatus.Synchronised
            || catchRecord.MetadataSyncStatus != SyncStatus.Synchronised;
    }
}
