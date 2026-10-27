using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Features.Authentication.Providers;

public interface IAccessTokenProvider
{
    Task<ApiAccessTokenModel> GetAccessTokenAsync(CancellationToken cancellationToken);
}
