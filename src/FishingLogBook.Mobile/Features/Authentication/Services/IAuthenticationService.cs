using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Providers;

namespace FishingLogBook.Mobile.Features.Authentication.Services;

public interface IAuthenticationService : IAccessTokenProvider
{
    event EventHandler? SessionChanged;
    event EventHandler<AuthenticationResultEnum>? SignInCompleted;

    AuthenticationSessionModel? CurrentSession { get; }
    bool HasPendingAuthorization { get; }

    Task<AuthenticationResultEnum> InitializeAsync(CancellationToken cancellationToken);
    Task<AuthenticationResultEnum> BeginSignInAsync(CancellationToken cancellationToken);
    Task<AuthenticationResultEnum> CompleteSignInAsync(Uri callback, CancellationToken cancellationToken);
    Task CancelSignInAsync();
    Task SignOutAsync(CancellationToken cancellationToken);
}
