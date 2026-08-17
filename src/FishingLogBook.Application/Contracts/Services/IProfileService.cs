using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface IProfileService
{
    Task<Result<ProfileDto>> GetOwnAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<ProfileDto>> UpdateOwnAsync(UpdateProfileArgs args, CancellationToken cancellationToken);

    Task<Result<PublicProfileDto>> GetPublicAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<PhotographUploadDto>> CreatePhotographUploadAsync(
        Guid userId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken);

    Task<Result<ProfileDto>> RecordPhotographAsync(
        RecordProfilePhotographArgs args,
        CancellationToken cancellationToken);

    bool IsObjectStorageConfigured { get; }
}
