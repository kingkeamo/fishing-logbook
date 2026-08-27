using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;

public sealed class ThrowingTripNoteStore : ITripNoteStore
{
    public Task SaveAsync(TripNoteModel note, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("IndexedDB unavailable.");
    }

    public Task<bool> DeleteAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("IndexedDB unavailable.");
    }

    public Task<IReadOnlyList<TripNoteModel>> GetForTripAsync(
        Guid ownerUserId,
        Guid tripId,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("IndexedDB unavailable.");
    }

    public Task<TripNoteModel?> GetAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("IndexedDB unavailable.");
    }

    public Task<IReadOnlyList<TripNoteModel>> GetPendingAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("IndexedDB unavailable.");
    }

    public Task<IReadOnlyCollection<Guid>> GetTripsWithPendingNotesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("IndexedDB unavailable.");
    }
}
