using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

/// <summary>Removes a note or a catch from a trip. Both return false when nothing was removed, so the caller can tell the angler.</summary>
public interface ITripRemovalService
{
    Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId, CancellationToken cancellationToken);

    Task<bool> RemoveCatchAsync(Guid catchId, CancellationToken cancellationToken);
}
