using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Profile.Clients;

public interface IFishingPreferenceClient
{
    Task<FishingCatalogueDto> GetCatalogueAsync(CancellationToken cancellationToken);

    Task<FishingPreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken);

    Task<FishingPreferencesDto> UpdatePreferencesAsync(
        UpdateFishingPreferencesDto preferences,
        CancellationToken cancellationToken);
}
