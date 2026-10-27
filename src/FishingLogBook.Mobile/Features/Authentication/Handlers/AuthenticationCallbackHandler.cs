using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Services;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Authentication.Handlers;

public sealed class AuthenticationCallbackHandler
{
    private readonly Uri _loginCallback;
    private readonly Uri _logoutCallback;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<AuthenticationCallbackHandler> _logger;

    public AuthenticationCallbackHandler(
        MobileAuthConfig config,
        IAuthenticationService authenticationService,
        ILogger<AuthenticationCallbackHandler> logger)
    {
        _loginCallback = new Uri(config.RedirectUri);
        _logoutCallback = new Uri(config.LogoutRedirectUri);
        _authenticationService = authenticationService;
        _logger = logger;
    }

    public async Task HandleAsync(Uri callback)
    {
        if (Matches(callback, _logoutCallback))
        {
            return;
        }

        if (!Matches(callback, _loginCallback))
        {
            _logger.LogWarning("Ignored an unrecognised native authentication callback.");
            return;
        }

        try
        {
            await _authenticationService.CompleteSignInAsync(callback, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Native authentication callback handling failed.");
        }
    }

    private static bool Matches(Uri callback, Uri expected)
    {
        return string.Equals(callback.Scheme, expected.Scheme, StringComparison.Ordinal) &&
            string.Equals(callback.Host, expected.Host, StringComparison.Ordinal) &&
            string.Equals(callback.AbsolutePath, expected.AbsolutePath, StringComparison.Ordinal);
    }
}
