using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Profile.Services;

public interface IProfileClient
{
    Task<ProfileDto> GetOwnAsync(CancellationToken cancellationToken);

    Task<ProfileDto> UpdateOwnAsync(UpdateProfileDto profile, CancellationToken cancellationToken);

    Task<PublicProfileDto> GetPublicAsync(Guid userId, CancellationToken cancellationToken);

    Task<PhotographUploadDto> CreatePhotographUploadAsync(
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken);

    Task UploadPhotographAsync(string uploadUrl, byte[] bytes, string contentType, CancellationToken cancellationToken);

    Task<ProfileDto> RecordPhotographAsync(RecordPhotographDto request, CancellationToken cancellationToken);
}
