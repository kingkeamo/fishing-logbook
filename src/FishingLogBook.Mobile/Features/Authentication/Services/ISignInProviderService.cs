using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Features.Authentication.Services;

public interface ISignInProviderService
{
    IReadOnlyList<SignInProviderEnum> AvailableProviders { get; }
}
