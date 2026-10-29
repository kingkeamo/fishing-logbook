using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Features.Authentication.Services;

public interface IOidcRefreshService
{
    Task<OidcTokenResultModel> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
