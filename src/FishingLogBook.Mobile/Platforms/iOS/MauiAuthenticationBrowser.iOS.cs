using AuthenticationServices;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using FishingLogBook.Mobile.Native.Providers;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Native.Browsers;

public sealed partial class MauiAuthenticationBrowser
{
    private static ASWebAuthenticationSession? _session;

    private static partial void OpenOnMainThread(Uri uri, Uri callbackUri)
    {
        _session?.Cancel();
        var target = new NSUrl(uri.AbsoluteUri);
        var session = CreateSession(target, callbackUri);
        session.PresentationContextProvider = new WindowPresentationContextProvider();
        _session = session;
        if (!session.Start())
        {
            _session = null;
            throw new InvalidOperationException("The native authentication session could not be started.");
        }
    }

    private static ASWebAuthenticationSession CreateSession(NSUrl target, Uri callbackUri)
    {
        if (callbackUri.Scheme == Uri.UriSchemeHttps && OperatingSystem.IsIOSVersionAtLeast(17, 4))
        {
            return new ASWebAuthenticationSession(
                target,
                ASWebAuthenticationSessionCallback.Create(callbackUri.Host, callbackUri.AbsolutePath),
                OnSessionCompleted);
        }

        if (callbackUri.Scheme == Uri.UriSchemeHttps)
        {
            throw new PlatformNotSupportedException("Verified HTTPS authentication callbacks require iOS 17.4 or later.");
        }

        return new ASWebAuthenticationSession(target, callbackUri.Scheme, OnSessionCompleted);
    }

    private static async void OnSessionCompleted(NSUrl? callbackUrl, NSError? error)
    {
        _session = null;
        try
        {
            var handler = IPlatformApplication.Current?.Services.GetService<AuthenticationCallbackHandler>();
            if (handler is null)
            {
                return;
            }

            var callbackUri = callbackUrl?.AbsoluteString;
            if (error is null && handler.CanForward(callbackUri))
            {
                await handler.ForwardAsync(callbackUri);
                return;
            }

            await handler.AbandonAsync();
        }
        catch (Exception exception)
        {
            IPlatformApplication.Current?.Services.GetService<ILogger<MauiAuthenticationBrowser>>()?.LogWarning(
                "Native authentication session completion failed with {ExceptionType}.",
                exception.GetType().FullName);
        }
    }
}
