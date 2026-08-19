using FishingLogBook.Web.Features.Profile.Models;

namespace FishingLogBook.Web.Features.Profile.Providers;

public interface IAnglerPreferencesProvider
{
    Task<AnglerPreferencesModel> GetAsync(CancellationToken cancellationToken);

    Task SetAsync(
        Guid userId,
        AnglerPreferencesModel preferences,
        CancellationToken cancellationToken);
}
