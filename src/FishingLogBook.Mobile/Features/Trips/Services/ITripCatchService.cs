using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface ITripCatchService
{
    Task<IReadOnlyList<TripCatchCandidateModel>> GetEligibleAsync(LocalTripRecordModel trip, CancellationToken cancellationToken);

    Task<TripCatchChangeModel> AssociateAsync(LocalTripRecordModel trip, IReadOnlyList<Guid> catchIds, CancellationToken cancellationToken);

    Task<bool> RemoveAsync(Guid catchId, CancellationToken cancellationToken);
}
