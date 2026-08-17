using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface ICatchService
{
    Task<Result<CatchDto>> UpsertAsync(UpsertCatchArgs args, CancellationToken cancellationToken);
}
