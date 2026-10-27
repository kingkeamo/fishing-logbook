using FishingLogBook.Mobile.Configuration;

namespace FishingLogBook.Mobile.Tests.Support;

public static class TestMobileAuthConfig
{
    public const string ApiBaseUrl = "https://api.example.com";

    public static MobileAuthConfig Create()
    {
        return new MobileAuthConfig
        {
            Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_pool",
            ClientId = "client123",
            HostedUiDomain = "https://auth.example.com",
            ApiBaseUrl = ApiBaseUrl,
            ApiScope = "https://api.example.com/access",
            ApiResource = "https://api.example.com",
            RedirectUri = "https://fishing-logbook-dev.pages.dev/native/oauth2redirect",
            LogoutRedirectUri = "https://fishing-logbook-dev.pages.dev/native/signedout",
            SchemeRedirectUri = "com.fishinglogbook.mobile.demo://oauth2redirect",
            SchemeLogoutRedirectUri = "com.fishinglogbook.mobile.demo://signedout"
        };
    }
}
