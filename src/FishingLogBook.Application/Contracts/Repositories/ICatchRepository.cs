using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface ICatchRepository
{
    Task<Result<Catch?>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<CatchPhotograph?>> GetPhotographAsync(
        GetCatchPhotographArgs args,
        CancellationToken cancellationToken);

    Task<Result<Catch>> UpsertAsync(Catch catchRecord, CancellationToken cancellationToken);

    Task<Result> UpdateLocationVisibilityAsync(
        PersistCatchLocationVisibilityArgs args,
        CancellationToken cancellationToken);
}
