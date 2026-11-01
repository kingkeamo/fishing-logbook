using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface ITripPhotographService
{
    /// <summary>Takes or picks one photograph and saves it to the trip. Returns a failure text key, or null when it succeeded or was cancelled.</summary>
    Task<string?> AddAsync(LocalTripRecordModel trip, bool useCamera, CancellationToken cancellationToken);

    /// <summary>Removes one of the viewer's own photographs from the trip. Returns false when it cannot be removed.</summary>
    Task<bool> RemoveAsync(LocalTripRecordModel trip, Guid photographId, CancellationToken cancellationToken);
}
