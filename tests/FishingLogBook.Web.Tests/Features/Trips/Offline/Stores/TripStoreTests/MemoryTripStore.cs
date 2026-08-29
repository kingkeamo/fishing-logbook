using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripStoreTests;

public sealed class MemoryTripStore : ITripStore
{
    private readonly Dictionary<Guid, TripModel> _trips = [];

    public bool FailWrite { get; set; }

    public bool FailCleanup { get; set; }

    public int PendingCalls { get; private set; }

    public int CleanupCalls { get; private set; }

    public IReadOnlyCollection<Guid> RetainedTripIds { get; private set; } = [];

    public Func<Guid, Task>? BeforeSingleRead { get; set; }

    public Task HydrateAsync(TripModel trip, Guid viewerUserId, CancellationToken cancellationToken)
    {
        if (!trip.CanContribute(viewerUserId))
        {
            throw new InvalidOperationException("A shared trip can only be cached for a contributor.");
        }

        _trips[trip.Id] = trip;
        return Task.CompletedTask;
    }

    public Task SaveAsync(TripModel trip, CancellationToken cancellationToken)
    {
        if (trip.OwnerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip requires an owner.");
        }

        if (FailWrite)
        {
            throw new InvalidOperationException("Trip persistence failed.");
        }

        _trips[trip.Id] = trip;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TripModel>> GetAllAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TripModel>>(
            _trips.Values.Where(trip => trip.CanContribute(viewerUserId)).ToList());
    }

    public async Task<TripModel?> GetAsync(
        Guid viewerUserId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        if (BeforeSingleRead is not null)
        {
            await BeforeSingleRead(tripId);
        }

        return _trips.TryGetValue(tripId, out var trip) && trip.CanContribute(viewerUserId)
            ? trip
            : null;
    }

    public Task<TripModel?> GetActiveAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        return Task.FromResult(
            _trips.Values.FirstOrDefault(trip =>
                trip.OwnerUserId == ownerUserId && trip.Status == TripConstants.Active));
    }

    public Task<IReadOnlyList<TripModel>> GetPendingAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        PendingCalls++;
        return Task.FromResult<IReadOnlyList<TripModel>>(
            _trips.Values
                .Where(trip =>
                    trip.IsOwnedBy(ownerUserId)
                    && trip.Origin == TripOriginEnum.Local
                    && trip.SyncStatus != SyncStatus.Synchronised)
                .OrderBy(trip => trip.StartedOn)
                .ToList());
    }

    public Task<int> CleanupSyncedAsync(
        Guid viewerUserId,
        DateTimeOffset olderThan,
        IReadOnlyCollection<Guid> retainedTripIds,
        CancellationToken cancellationToken)
    {
        CleanupCalls++;
        RetainedTripIds = retainedTripIds;
        if (FailCleanup)
        {
            throw new InvalidOperationException("Trip cleanup failed.");
        }

        var removable = _trips.Values
            .Where(trip =>
                trip.CanContribute(viewerUserId)
                && trip.Status == TripConstants.Completed
                && trip.SyncStatus == SyncStatus.Synchronised
                && trip.SyncedAt is not null
                && trip.SyncedAt <= olderThan
                && !retainedTripIds.Contains(trip.Id))
            .Select(trip => trip.Id)
            .ToList();

        foreach (var tripId in removable)
        {
            _trips.Remove(tripId);
        }

        return Task.FromResult(removable.Count);
    }
}
