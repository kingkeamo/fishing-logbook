using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Clients;

public interface IProfileClient
{
    Task<ApiResultModel<ProfileDto>> GetAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<ProfileDto>> UpdateAsync(UpdateProfileDto profile, CancellationToken cancellationToken);
    Task<ApiResultModel<ProfileDto>> CompleteOnboardingAsync(CancellationToken cancellationToken);
}
