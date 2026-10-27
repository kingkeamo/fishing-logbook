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

    public bool CanForward(string? callbackUri)
    {
        return TryParseCallback(callbackUri, out _);
    }

    public async Task ForwardAsync(string? callbackUri)
    {
        if (!TryParseCallback(callbackUri, out var callback))
        {
            return;
        }

        try
        {
            await HandleAsync(callback);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Native authentication callback forwarding failed with {ExceptionType}.",
                exception.GetType().FullName);
        }
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
            _logger.LogWarning(
                "Native authentication callback handling failed with {ExceptionType}.",
                exception.GetType().FullName);
        }
    }

    private bool TryParseCallback(string? callbackUri, out Uri callback)
    {
        if (Uri.TryCreate(callbackUri, UriKind.Absolute, out var parsed) &&
            (Matches(parsed, _loginCallback) || Matches(parsed, _logoutCallback)))
        {
            callback = parsed;
            return true;
        }

        callback = _loginCallback;
        return false;
    }

    private static bool Matches(Uri callback, Uri expected)
    {
        return string.Equals(callback.Scheme, expected.Scheme, StringComparison.Ordinal) &&
            string.Equals(callback.Host, expected.Host, StringComparison.Ordinal) &&
            string.Equals(callback.AbsolutePath, expected.AbsolutePath, StringComparison.Ordinal);
    }
}
