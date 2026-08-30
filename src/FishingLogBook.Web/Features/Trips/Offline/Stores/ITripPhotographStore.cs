using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Offline.Stores;

public interface ITripPhotographStore
{
    Task SaveAsync(TripPhotographModel photograph, CancellationToken cancellationToken);

    Task<byte[]?> GetBytesAsync(
        Guid viewerUserId,
        Guid tripId,
        Guid photographId,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        Guid viewerUserId,
        Guid tripId,
        Guid photographId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TripPhotographModel>> GetPendingAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetTripsWithPendingPhotographsAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken);
}
