using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Offline.Stores;

public interface ITripNoteStore
{
    Task SaveAsync(TripNoteModel note, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TripNoteModel>> GetForTripAsync(
        Guid ownerUserId,
        Guid tripId,
        CancellationToken cancellationToken);

    Task<TripNoteModel?> GetAsync(
        Guid ownerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TripNoteModel>> GetPendingAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetTripsWithPendingNotesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken);
}
