using FishingLogBook.Web.Features.Profile.Models;

namespace FishingLogBook.Web.Features.Profile.Providers;

public interface IProfileSummaryProvider
{
    event Action? Changed;

    Task<ProfileSummaryModel> GetAsync(CancellationToken cancellationToken);

    Task RefreshAsync(CancellationToken cancellationToken);

    void Invalidate();
}
