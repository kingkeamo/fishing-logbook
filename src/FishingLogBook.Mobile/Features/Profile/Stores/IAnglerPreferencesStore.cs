using FishingLogBook.Mobile.Features.Profile.Models;

namespace FishingLogBook.Mobile.Features.Profile.Stores;

public interface IAnglerPreferencesStore
{
    Task<AnglerPreferencesModel?> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveAsync(Guid userId, AnglerPreferencesModel preferences, CancellationToken cancellationToken);
}
