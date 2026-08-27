using FishingLogBook.Domain.Trips;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface ITripPhotographRepository
{
    Task<Result<TripPhotograph?>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripPhotograph>>> GetByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken);

    Task<Result<TripPhotograph>> UpsertAsync(TripPhotograph photograph, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
