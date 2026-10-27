using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Browsers;

namespace FishingLogBook.Mobile.Features.Authentication.Clients;

public sealed class CognitoSessionClient : ICognitoSessionClient
{
    private readonly IAuthenticationBrowser _browser;
    private readonly MobileAuthConfig _config;
    private readonly HttpClient _httpClient;

    public CognitoSessionClient(
        HttpClient httpClient,
        IAuthenticationBrowser browser,
        MobileAuthConfig config)
    {
        _httpClient = httpClient;
        _browser = browser;
        _config = config;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = refreshToken,
            ["client_id"] = _config.ClientId
        });
        using var response = await _httpClient.PostAsync("oauth2/revoke", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task OpenLogoutAsync(CancellationToken cancellationToken)
    {
        var logoutUrl = new Uri(
            $"{_config.HostedUiDomain.TrimEnd('/')}/logout" +
            $"?client_id={Uri.EscapeDataString(_config.ClientId)}" +
            $"&logout_uri={Uri.EscapeDataString(_config.LogoutRedirectUri)}");
        await _browser.OpenAsync(logoutUrl, cancellationToken);
    }
}
