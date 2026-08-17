using FishingLogBook.Domain.Catches;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface ICatchRepository
{
    Task<Result<Catch?>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<Catch>> UpsertAsync(Catch catchRecord, CancellationToken cancellationToken);
}
