using FishingLogBook.Domain.Trips;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface ITripRepository
{
    Task<Result<Trip?>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripSummary>>> GetSummariesByOwnerUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripCatchSummary>>> GetCatchSummariesByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken);

    Task<Result<Trip>> UpsertAsync(Trip trip, CancellationToken cancellationToken);
}
