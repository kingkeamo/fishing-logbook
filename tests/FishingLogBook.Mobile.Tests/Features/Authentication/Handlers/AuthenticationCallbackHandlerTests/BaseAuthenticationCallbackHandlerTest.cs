using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Handlers.AuthenticationCallbackHandlerTests;

public abstract class BaseAuthenticationCallbackHandlerTest
{
    protected const string LoginCallback = "https://fishing-logbook-dev.pages.dev/native/oauth2redirect?code=secret-code&state=secret-state";
    protected const string LogoutCallback = "https://fishing-logbook-dev.pages.dev/native/signedout";
    protected readonly IAuthenticationService MockAuthenticationService = Substitute.For<IAuthenticationService>();
    protected readonly CapturingLogger<AuthenticationCallbackHandler> Logger = new();
    protected readonly AuthenticationCallbackHandler Sut;

    protected BaseAuthenticationCallbackHandlerTest()
    {
        Sut = new AuthenticationCallbackHandler(
            new MobileAuthConfig
            {
                Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_pool",
                ClientId = "client123",
                HostedUiDomain = "https://auth.example.com",
                ApiBaseUrl = "https://api.example.com",
                ApiScope = "https://api.example.com/access",
                ApiResource = "https://api.example.com",
                RedirectUri = "https://fishing-logbook-dev.pages.dev/native/oauth2redirect",
                LogoutRedirectUri = LogoutCallback,
                SchemeRedirectUri = "com.fishinglogbook.mobile.demo://oauth2redirect",
                SchemeLogoutRedirectUri = "com.fishinglogbook.mobile.demo://signedout"
            },
            MockAuthenticationService,
            Logger);
    }
}
