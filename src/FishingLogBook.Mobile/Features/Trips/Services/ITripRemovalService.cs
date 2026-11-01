using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

/// <summary>
/// Removes a note or a catch from a trip: asks the angler to confirm, removes it, and reports a failure.
/// Both return true only when something was removed, so a decline, a missing note and a failure are all false.
/// </summary>
public interface ITripRemovalService
{
    Task<bool> RemoveNoteAsync(LocalTripRecordModel trip, Guid noteId, CancellationToken cancellationToken);

    Task<bool> RemoveCatchAsync(Guid catchId, string catchLabel, CancellationToken cancellationToken);
}
