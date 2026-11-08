using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Features.Authentication.Services;

public interface ISignInNavigationService
{
    IReadOnlyList<SignInProviderEnum> Providers { get; }

    void BeginSignIn(string? returnUrl = null);

    void SignInWith(SignInProviderEnum provider, string? returnUrl = null);
}
