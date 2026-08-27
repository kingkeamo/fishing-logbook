using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface ITripPhotographService
{
    bool IsObjectStorageConfigured { get; }

    Task<Result<PhotographUploadDto>> CreateUploadAsync(
        CreateTripPhotographUploadArgs args,
        CancellationToken cancellationToken);

    Task<Result<TripPhotographDto>> RecordAsync(
        RecordTripPhotographArgs args,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(DeleteTripPhotographArgs args, CancellationToken cancellationToken);
}
