using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Clients;

public interface IFishingLocationClient
{
    Task<ApiResultModel<FishingLocationPreferencesDto>> GetAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<FishingLocationPreferencesDto>> UpdateAsync(
        UpdateFishingLocationPreferencesDto locations,
        CancellationToken cancellationToken);
}
