using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;

namespace FishingLogBook.Mobile.Features.Profile.Services;

public interface IAnglerPreferencesService
{
    Task<AnglerPreferencesModel?> GetCachedAsync(CancellationToken cancellationToken);
    Task<ApiResultModel<AnglerPreferencesModel>> RefreshAsync(CancellationToken cancellationToken);
}
