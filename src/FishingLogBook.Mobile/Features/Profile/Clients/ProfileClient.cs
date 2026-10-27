using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Clients;

public sealed class ProfileClient(AuthorizedApiClient apiClient) : IProfileClient
{
    private const string ProfilePath = "api/profiles/me";
    private const string OnboardingPath = "api/profiles/me/onboarding";

    public Task<ApiResultModel<ProfileDto>> GetAsync(CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<ProfileDto>(ProfilePath, cancellationToken);
    }

    public Task<ApiResultModel<ProfileDto>> UpdateAsync(
        UpdateProfileDto profile,
        CancellationToken cancellationToken)
    {
        return apiClient.PutAsync<UpdateProfileDto, ProfileDto>(ProfilePath, profile, cancellationToken);
    }

    public Task<ApiResultModel<ProfileDto>> CompleteOnboardingAsync(CancellationToken cancellationToken)
    {
        return apiClient.PutAsync<ProfileDto>(OnboardingPath, cancellationToken);
    }
}
