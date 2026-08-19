using FishingLogBook.Web.Features.Profile.Models;

namespace FishingLogBook.Web.Features.Profile.Services;

public interface IAnglerPreferencesProvider
{
    Task<AnglerPreferencesModel> GetAsync(CancellationToken cancellationToken);

    Task SetAsync(
        Guid userId,
        AnglerPreferencesModel preferences,
        CancellationToken cancellationToken);
}
