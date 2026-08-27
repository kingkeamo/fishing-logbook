using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Profile.Clients;

public interface IFishingLocationClient
{
    Task<FishingLocationPreferencesDto> GetAsync(CancellationToken cancellationToken);

    Task<FishingLocationPreferencesDto> UpdateAsync(
        UpdateFishingLocationPreferencesDto locations,
        CancellationToken cancellationToken);
}
