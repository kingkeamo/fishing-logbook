using FishingLogBook.Web.Features.Profile.Models;

namespace FishingLogBook.Web.Features.Profile.Offline.Stores;

public interface IAnglerPreferencesStore
{
    Task<AnglerPreferencesModel?> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveAsync(Guid userId, AnglerPreferencesModel preferences, CancellationToken cancellationToken);
}
