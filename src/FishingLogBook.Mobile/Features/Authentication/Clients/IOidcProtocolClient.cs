using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Features.Authentication.Clients;

public interface IOidcProtocolClient
{
    Task<PendingAuthorizationModel> PrepareLoginAsync(CancellationToken cancellationToken);
    Task<OidcTokenResultModel> CompleteLoginAsync(
        Uri callback,
        PendingAuthorizationModel pendingAuthorization,
        CancellationToken cancellationToken);
    Task<OidcTokenResultModel> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
