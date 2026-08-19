using FishingLogBook.Web.Features.Profile.Models;

namespace FishingLogBook.Web.Features.Profile.Providers;

public interface IProfileSummaryProvider
{
    Task<ProfileSummaryModel> GetAsync(CancellationToken cancellationToken);

    void Invalidate();
}
