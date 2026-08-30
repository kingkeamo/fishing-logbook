using FishingLogBook.Domain.Trips;
using FluentResults;

namespace FishingLogBook.Application.Trips.Contracts.Repositories;

public interface ITripNoteRepository
{
    Task<Result<TripNote?>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripNote>>> GetByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken);

    Task<Result<TripNote>> UpsertAsync(TripNote note, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
