using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Browsers;
using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Clients.CognitoSessionClientTests;

public abstract class BaseCognitoSessionClientTest
{
    protected readonly IAuthenticationBrowser MockBrowser = Substitute.For<IAuthenticationBrowser>();

    protected (CognitoSessionClient Client, RecordingHttpMessageHandler Server) CreateSut(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var server = new RecordingHttpMessageHandler(respond);
        var httpClient = new HttpClient(server)
        {
            BaseAddress = new Uri("https://auth.example.com/")
        };
        var config = new MobileAuthConfig
        {
            Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_pool",
            ClientId = "client-123",
            HostedUiDomain = "https://auth.example.com/",
            ApiBaseUrl = "https://api.example.com",
            ApiScope = "https://api.example.com/access",
            ApiResource = "https://api.example.com",
            RedirectUri = "https://fishing-logbook-dev.pages.dev/native/oauth2redirect",
            LogoutRedirectUri = "https://fishing-logbook-dev.pages.dev/native/signedout",
            SchemeRedirectUri = "com.fishinglogbook.mobile.demo://oauth2redirect",
            SchemeLogoutRedirectUri = "com.fishinglogbook.mobile.demo://signedout"
        };
        return (new CognitoSessionClient(httpClient, MockBrowser, config), server);
    }

    protected (CognitoSessionClient Client, RecordingHttpMessageHandler Server) CreateSut(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return CreateSut((request, _) => Task.FromResult(respond(request)));
    }
}
