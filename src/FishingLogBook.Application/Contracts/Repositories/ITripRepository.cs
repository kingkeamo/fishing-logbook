using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Trips;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface ITripRepository
{
    Task<Result<Trip?>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripSummary>>> GetSummariesForUserAsync(
        GetMyTripsArgs args,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripCatchSummary>>> GetCatchSummariesByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken);

    Task<Result<Trip>> UpsertAsync(Trip trip, CancellationToken cancellationToken);
}
