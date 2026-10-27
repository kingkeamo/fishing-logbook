using System.Reflection;
using System.Text.Json;

namespace FishingLogBook.Mobile.Configuration;

public sealed record MobileAuthConfig
{
    private const string ApplicationScheme = "com.fishinglogbook.mobile.demo";
    private const string AppLinkCallbackPath = "/native/oauth2redirect";
    private const string AppLinkSignedOutPath = "/native/signedout";

    public required string Authority { get; init; }
    public required string ClientId { get; init; }
    public required string HostedUiDomain { get; init; }
    public required string ApiBaseUrl { get; init; }
    public required string ApiScope { get; init; }
    public required string ApiResource { get; init; }
    public required string RedirectUri { get; init; }
    public required string LogoutRedirectUri { get; init; }
    public required string SchemeRedirectUri { get; init; }
    public required string SchemeLogoutRedirectUri { get; init; }

    public static MobileAuthConfig Load(Assembly? assembly = null)
    {
        assembly ??= typeof(MobileAuthConfig).Assembly;
        using var stream = assembly.GetManifestResourceStream("FishingLogBook.Mobile.mobileauth.json")
            ?? throw new InvalidOperationException("The mobile authentication configuration is missing.");
        var config = JsonSerializer.Deserialize<MobileAuthConfig>(stream)
            ?? throw new InvalidOperationException("The mobile authentication configuration is invalid.");
        config.EnsureRequired();
        return config;
    }

    public MobileAuthConfig WithSchemeCallbacks()
    {
        return this with
        {
            RedirectUri = SchemeRedirectUri,
            LogoutRedirectUri = SchemeLogoutRedirectUri
        };
    }

    public void EnsureRequired()
    {
        var authority = RequireHttpsOrigin(Authority, nameof(Authority), allowPath: true);
        RequireHttpsOrigin(HostedUiDomain, nameof(HostedUiDomain), allowPath: false);
        var apiBase = RequireHttpsOrigin(ApiBaseUrl, nameof(ApiBaseUrl), allowPath: true);
        var apiResource = RequireHttpsOrigin(ApiResource, nameof(ApiResource), allowPath: true);
        var redirect = RequireAppLinkRedirect(RedirectUri, nameof(RedirectUri), AppLinkCallbackPath);
        var logoutRedirect = RequireAppLinkRedirect(LogoutRedirectUri, nameof(LogoutRedirectUri), AppLinkSignedOutPath);
        RequireCustomRedirect(SchemeRedirectUri, nameof(SchemeRedirectUri), "oauth2redirect");
        RequireCustomRedirect(SchemeLogoutRedirectUri, nameof(SchemeLogoutRedirectUri), "signedout");

        if (string.IsNullOrWhiteSpace(ClientId) || ClientId.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException($"{nameof(ClientId)} must be a non-empty Cognito public client ID.");
        }

        if (!string.Equals(apiBase.GetLeftPart(UriPartial.Path).TrimEnd('/'), apiResource.GetLeftPart(UriPartial.Path).TrimEnd('/'), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{nameof(ApiResource)} must match {nameof(ApiBaseUrl)}.");
        }

        if (!ApiScope.StartsWith(ApiResource.TrimEnd('/') + "/", StringComparison.Ordinal) || ApiScope.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException($"{nameof(ApiScope)} must be a scope belonging to {nameof(ApiResource)}.");
        }

        if (!string.Equals(redirect.Host, logoutRedirect.Host, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{nameof(RedirectUri)} and {nameof(LogoutRedirectUri)} must use the same verified host.");
        }

        if (!authority.AbsolutePath.Contains('_', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Cognito authority is invalid.");
        }
    }

    private static Uri RequireHttpsOrigin(string value, string name, bool allowPath)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            (!allowPath && uri.AbsolutePath != "/"))
        {
            throw new InvalidOperationException($"{name} must be a valid HTTPS endpoint.");
        }

        return uri;
    }

    private static Uri RequireAppLinkRedirect(string value, string name, string expectedPath)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.Equals(uri.AbsolutePath, expectedPath, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"{name} must be the verified HTTPS application callback.");
        }

        return uri;
    }

    private static void RequireCustomRedirect(string value, string name, string expectedHost)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != ApplicationScheme ||
            !string.Equals(uri.Host, expectedHost, StringComparison.Ordinal) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"{name} must use the registered application callback.");
        }
    }
}
