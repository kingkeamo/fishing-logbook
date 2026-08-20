using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface ICatchService
{
    Task<Result<CatchDto>> UpsertAsync(UpsertCatchArgs args, CancellationToken cancellationToken);

    Task<Result<CatchViewDto>> GetViewAsync(GetCatchArgs args, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<CatchViewDto>>> GetMyAsync(GetMyCatchesArgs args, CancellationToken cancellationToken);

    Task<Result> UpdateLocationVisibilityAsync(
        UpdateCatchLocationVisibilityArgs args,
        CancellationToken cancellationToken);
}
