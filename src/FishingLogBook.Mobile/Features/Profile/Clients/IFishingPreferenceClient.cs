using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Clients;

public interface IFishingPreferenceClient
{
    Task<ApiResultModel<FishingCatalogueDto>> GetCatalogueAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<FishingPreferencesDto>> GetPreferencesAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<FishingPreferencesDto>> UpdatePreferencesAsync(
        UpdateFishingPreferencesDto preferences,
        CancellationToken cancellationToken);
}
