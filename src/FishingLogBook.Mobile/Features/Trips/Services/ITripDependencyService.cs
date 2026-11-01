using FishingLogBook.Mobile.Common.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface ITripDependencyService
{
    Task<OutboxOperationResultModel?> CheckAsync(Guid userId, Guid tripId, CancellationToken cancellationToken);
}
