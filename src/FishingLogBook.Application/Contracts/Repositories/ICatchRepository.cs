using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface ICatchRepository
{
    Task<Result<Catch?>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<CatchDetail?>> GetDetailForUserAsync(
        Guid catchId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<CatchDetail>>> GetActivityForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<CatchPhotograph?>> GetPhotographAsync(
        GetCatchPhotographArgs args,
        CancellationToken cancellationToken);

    Task<Result> DeletePhotographAsync(
        GetCatchPhotographArgs args,
        CancellationToken cancellationToken);

    Task<Result<Catch>> UpsertAsync(Catch catchRecord, CancellationToken cancellationToken);

    Task<Result<bool>> AssociateTripAsync(
        PersistCatchTripArgs args,
        CancellationToken cancellationToken);

    Task<Result> UpdateLocationVisibilityAsync(
        PersistCatchLocationVisibilityArgs args,
        CancellationToken cancellationToken);
}
