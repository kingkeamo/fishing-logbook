using FishingLogBook.Domain.Trips;
using FluentResults;

namespace FishingLogBook.Application.Trips.Contracts.Services;

public interface ITripAccessService
{
    Task<Result<TripAccess>> ResolveAsync(Guid tripId, CancellationToken cancellationToken);

    Task<Result<TripAccess>> ResolveForAsync(
        Guid tripId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<TripAccess>> RequireContributorAsync(Guid tripId, CancellationToken cancellationToken);

    Task<Result<TripAccess>> RequireOwnerAsync(Guid tripId, CancellationToken cancellationToken);
}
