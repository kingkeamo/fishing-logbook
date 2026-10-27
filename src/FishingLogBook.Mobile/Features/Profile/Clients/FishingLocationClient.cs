using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Clients;

public sealed class FishingLocationClient(AuthorizedApiClient apiClient) : IFishingLocationClient
{
    private const string LocationsPath = "api/profiles/me/fishing-locations";

    public Task<ApiResultModel<FishingLocationPreferencesDto>> GetAsync(CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<FishingLocationPreferencesDto>(LocationsPath, cancellationToken);
    }

    public Task<ApiResultModel<FishingLocationPreferencesDto>> UpdateAsync(
        UpdateFishingLocationPreferencesDto locations,
        CancellationToken cancellationToken)
    {
        return apiClient.PutAsync<UpdateFishingLocationPreferencesDto, FishingLocationPreferencesDto>(
            LocationsPath,
            locations,
            cancellationToken);
    }
}
