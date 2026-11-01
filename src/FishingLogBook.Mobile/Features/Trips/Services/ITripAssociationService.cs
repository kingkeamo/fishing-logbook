using FishingLogBook.Mobile.Features.Trips.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface ITripAssociationService
{
    /// <summary>
    /// Resolves the trip a new catch should be recorded in: the requested trip when given,
    /// otherwise the account's active trip. Works from the local cache when offline.
    /// </summary>
    Task<TripAssociationModel?> ResolveAsync(Guid? requestedTripId, CancellationToken cancellationToken);

    /// <summary>True when the signed-in account can still contribute to the trip locally.</summary>
    Task<bool> IsAvailableAsync(Guid tripId, CancellationToken cancellationToken);
}
