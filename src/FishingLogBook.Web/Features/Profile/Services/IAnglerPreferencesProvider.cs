using FishingLogBook.Web.Features.Profile.Models;

namespace FishingLogBook.Web.Features.Profile.Services;

public interface IAnglerPreferencesProvider
{
    Task<AnglerPreferencesModel> GetAsync(CancellationToken cancellationToken);
}
