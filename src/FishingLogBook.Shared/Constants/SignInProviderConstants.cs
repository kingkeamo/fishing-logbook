using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Constants;

public static class SignInProviderConstants
{
    public const string IdentityProviderParameter = "identity_provider";
    public const string CognitoGoogle = "Google";
    public const string CognitoApple = "SignInWithApple";

    public static string? ToCognitoIdentityProvider(SignInProviderEnum provider)
    {
        return provider switch
        {
            SignInProviderEnum.Google => CognitoGoogle,
            SignInProviderEnum.Apple => CognitoApple,
            _ => null
        };
    }

    public static bool TryParse(string? value, out SignInProviderEnum provider)
    {
        provider = value?.Trim().ToLowerInvariant() switch
        {
            "google" => SignInProviderEnum.Google,
            "apple" => SignInProviderEnum.Apple,
            _ => SignInProviderEnum.EmailPassword
        };
        return provider != SignInProviderEnum.EmailPassword;
    }
}
