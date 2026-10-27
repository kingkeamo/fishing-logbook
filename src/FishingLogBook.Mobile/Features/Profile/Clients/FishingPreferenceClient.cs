using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Clients;

public sealed class FishingPreferenceClient(AuthorizedApiClient apiClient) : IFishingPreferenceClient
{
    private const string CataloguePath = "api/fishing-catalogue";
    private const string PreferencesPath = "api/profiles/me/fishing-preferences";

    public Task<ApiResultModel<FishingCatalogueDto>> GetCatalogueAsync(CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<FishingCatalogueDto>(CataloguePath, cancellationToken);
    }

    public Task<ApiResultModel<FishingPreferencesDto>> GetPreferencesAsync(CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<FishingPreferencesDto>(PreferencesPath, cancellationToken);
    }

    public Task<ApiResultModel<FishingPreferencesDto>> UpdatePreferencesAsync(
        UpdateFishingPreferencesDto preferences,
        CancellationToken cancellationToken)
    {
        return apiClient.PutAsync<UpdateFishingPreferencesDto, FishingPreferencesDto>(
            PreferencesPath,
            preferences,
            cancellationToken);
    }
}
