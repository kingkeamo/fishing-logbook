using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Offline.Stores;

public interface ITripNoteStore
{
    Task SaveAsync(TripNoteModel note, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        Guid viewerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TripNoteModel>> GetForTripAsync(
        Guid viewerUserId,
        Guid tripId,
        CancellationToken cancellationToken);

    Task<TripNoteModel?> GetAsync(
        Guid viewerUserId,
        Guid tripId,
        Guid noteId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TripNoteModel>> GetPendingAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetTripsWithPendingNotesAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken);
}
