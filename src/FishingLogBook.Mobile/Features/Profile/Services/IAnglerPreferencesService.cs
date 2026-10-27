using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Services;

public interface IAnglerPreferencesService
{
    Task<AnglerPreferencesModel?> GetCachedAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<AnglerPreferencesModel>> RefreshAsync(CancellationToken cancellationToken);
    Task<SaveResultModel<AnglerPreferencesModel>> SaveAsync(
        AccountSessionLeaseModel lease,
        FishingCatalogueDto catalogue,
        UpdateFishingPreferencesDto preferences,
        UpdateFishingLocationPreferencesDto locations,
        CancellationToken cancellationToken);
}
