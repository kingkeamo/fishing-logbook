using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Catches.Contracts.Services;

public interface ICatchPhotographService
{
    bool IsObjectStorageConfigured { get; }

    Task<Result<PhotographUploadDto>> CreateUploadAsync(
        CreateCatchPhotographUploadArgs args,
        CancellationToken cancellationToken);

    Task<Result> RecordAsync(
        RecordCatchPhotographArgs args,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(
        DeleteCatchPhotographArgs args,
        CancellationToken cancellationToken);
}
