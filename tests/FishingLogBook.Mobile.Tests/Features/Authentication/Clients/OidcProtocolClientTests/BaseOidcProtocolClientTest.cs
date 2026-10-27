using System.Net;
using System.Text;
using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Clients.OidcProtocolClientTests;

public abstract class BaseOidcProtocolClientTest
{
    protected const string Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_pool";
    protected const string HostedUiDomain = "https://auth.example.com";

    protected static OidcProtocolClient CreateSut(string endpointBaseAddress = HostedUiDomain)
    {
        return new OidcProtocolClient(
            CreateConfig(),
            new FixedTimeProvider(DateTimeOffset.UtcNow),
            new DiscoveryMessageHandler(endpointBaseAddress));
    }

    private static MobileAuthConfig CreateConfig()
    {
        return new MobileAuthConfig
        {
            Authority = Authority,
            ClientId = "client123",
            HostedUiDomain = HostedUiDomain,
            ApiBaseUrl = "https://api.example.com",
            ApiScope = "https://api.example.com/access",
            ApiResource = "https://api.example.com",
            RedirectUri = "com.fishinglogbook.mobile.demo://oauth2redirect",
            LogoutRedirectUri = "com.fishinglogbook.mobile.demo://signedout"
        };
    }

    private sealed class DiscoveryMessageHandler(string endpointBaseAddress) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsoluteUri;
            var body = path.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal)
                ? $$"""
                    {
                      "issuer": "{{Authority}}",
                      "authorization_endpoint": "{{endpointBaseAddress}}/oauth2/authorize",
                      "token_endpoint": "{{endpointBaseAddress}}/oauth2/token",
                      "revocation_endpoint": "{{endpointBaseAddress}}/oauth2/revoke",
                      "end_session_endpoint": "{{endpointBaseAddress}}/logout",
                      "jwks_uri": "{{Authority}}/.well-known/jwks.json",
                      "response_types_supported": ["code"],
                      "subject_types_supported": ["public"],
                      "id_token_signing_alg_values_supported": ["RS256"]
                    }
                    """
                : """{ "keys": [] }""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
