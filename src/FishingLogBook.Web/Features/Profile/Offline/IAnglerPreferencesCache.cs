using FishingLogBook.Web.Features.Profile.Models;

namespace FishingLogBook.Web.Features.Profile.Offline;

public interface IAnglerPreferencesCache
{
    Task<AnglerPreferencesModel?> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveAsync(Guid userId, AnglerPreferencesModel preferences, CancellationToken cancellationToken);
}
