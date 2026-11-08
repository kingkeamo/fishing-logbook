using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Features.Authentication.Clients;

public interface IOidcProtocolClient
{
    Task<PendingAuthorizationModel> PrepareLoginAsync(
        CancellationToken cancellationToken,
        SignInProviderEnum provider = SignInProviderEnum.EmailPassword);
    Task<OidcTokenResultModel> CompleteLoginAsync(
        Uri callback,
        PendingAuthorizationModel pendingAuthorization,
        CancellationToken cancellationToken);
    Task<OidcTokenResultModel> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
