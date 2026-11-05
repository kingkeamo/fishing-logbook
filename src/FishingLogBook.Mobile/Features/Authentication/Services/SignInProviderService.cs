using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Features.Authentication.Services;

public sealed class SignInProviderService : ISignInProviderService
{
    private const string IosPlatform = "iOS";
    private const string AndroidPlatform = "Android";

    public SignInProviderService(MobileAuthConfig config, IAppInfoAdapter appInfo)
    {
        var configured = config.SocialProviders
            .Select(value => SignInProviderConstants.TryParse(value, out var provider) ? provider : SignInProviderEnum.EmailPassword)
            .Where(provider => provider != SignInProviderEnum.EmailPassword)
            .Distinct()
            .ToHashSet();
        var platform = appInfo.Platform;
        var available = new List<SignInProviderEnum>();
        if (configured.Contains(SignInProviderEnum.Apple) && IsPlatform(platform, IosPlatform))
        {
            available.Add(SignInProviderEnum.Apple);
        }

        if (configured.Contains(SignInProviderEnum.Google) && (IsPlatform(platform, IosPlatform) || IsPlatform(platform, AndroidPlatform)))
        {
            available.Add(SignInProviderEnum.Google);
        }

        AvailableProviders = available;
    }

    public IReadOnlyList<SignInProviderEnum> AvailableProviders { get; }

    private static bool IsPlatform(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
