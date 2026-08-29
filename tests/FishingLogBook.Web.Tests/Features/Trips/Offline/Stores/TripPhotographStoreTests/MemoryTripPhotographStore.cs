using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripPhotographStoreTests;

public sealed class MemoryTripPhotographStore : ITripPhotographStore
{
    private readonly Dictionary<Guid, TripPhotographModel> _photographs = [];
    private readonly Dictionary<Guid, byte[]> _bytes = [];
    private readonly HashSet<Guid> _byteReads = [];

    public bool FailWrite { get; set; }

    public int PendingCalls { get; private set; }

    public int PendingTripCalls { get; private set; }

    public Func<Guid, Task>? BeforeByteRead { get; set; }

    public IReadOnlyCollection<Guid> BytesReadFor => _byteReads;

    public Task SaveAsync(TripPhotographModel photograph, CancellationToken cancellationToken)
    {
        if (photograph.ContributedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A trip photograph requires an owner.");
        }

        if (photograph.Bytes is not { Length: > 0 })
        {
            throw new InvalidOperationException("A trip photograph requires prepared bytes.");
        }

        if (FailWrite)
        {
            throw new InvalidOperationException("Trip photograph persistence failed.");
        }

        _bytes[photograph.Id] = photograph.Bytes;
        _photographs[photograph.Id] = photograph with { Bytes = null };
        return Task.CompletedTask;
    }

    public async Task<byte[]?> GetBytesAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        if (BeforeByteRead is not null)
        {
            await BeforeByteRead(photographId);
        }

        _byteReads.Add(photographId);
        if (!_photographs.TryGetValue(photographId, out var photograph)
            || photograph.ContributedByUserId != ownerUserId
            || photograph.TripId != tripId)
        {
            return null;
        }

        return _bytes.TryGetValue(photographId, out var bytes) ? bytes : null;
    }

    public Task<bool> DeleteAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        if (!_photographs.TryGetValue(photographId, out var photograph)
            || photograph.ContributedByUserId != ownerUserId
            || photograph.TripId != tripId)
        {
            return Task.FromResult(false);
        }

        _photographs.Remove(photographId);
        _bytes.Remove(photographId);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<TripPhotographModel>> GetPendingAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        PendingCalls++;
        return Task.FromResult<IReadOnlyList<TripPhotographModel>>(
            [.. _photographs.Values
                .Where(photograph =>
                    photograph.ContributedByUserId == ownerUserId
                    && photograph.SyncStatus != SyncStatus.Synchronised)
                .OrderBy(photograph => photograph.OrderedOn)]);
    }

    public Task<IReadOnlyCollection<Guid>> GetTripsWithPendingPhotographsAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        PendingTripCalls++;
        return Task.FromResult<IReadOnlyCollection<Guid>>(
            [.. _photographs.Values
                .Where(photograph =>
                    photograph.ContributedByUserId == ownerUserId
                    && photograph.SyncStatus != SyncStatus.Synchronised)
                .Select(photograph => photograph.TripId)
                .Distinct()]);
    }

    public TripPhotographModel? Stored(Guid photographId)
    {
        return _photographs.TryGetValue(photographId, out var photograph) ? photograph : null;
    }

    public int Count => _photographs.Count;

    public IReadOnlyList<TripPhotographModel> Pending()
    {
        return [.. _photographs.Values.Where(photograph => photograph.SyncStatus != SyncStatus.Synchronised)];
    }
}
