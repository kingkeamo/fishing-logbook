using AndroidX.Activity.Result;
using AndroidX.Browser.Auth;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using Microsoft.Extensions.DependencyInjection;
using AndroidLog = Android.Util.Log;

namespace FishingLogBook.Mobile.Native.Callbacks;

public sealed class AuthTabResultCallback : Java.Lang.Object, IActivityResultCallback
{
    public void OnActivityResult(Java.Lang.Object? result)
    {
        Dispatch(result as AuthTabIntent.AuthResult);
    }

    private static async void Dispatch(AuthTabIntent.AuthResult? result)
    {
        try
        {
            var handler = IPlatformApplication.Current?.Services.GetService<AuthenticationCallbackHandler>();
            if (handler is null)
            {
                return;
            }

            var callbackUri = result?.ResultUri?.ToString();
            if (result?.ResultCode == AuthTabIntent.ResultOk && handler.CanForward(callbackUri))
            {
                await handler.ForwardAsync(callbackUri);
                return;
            }

            AndroidLog.Info(
                AndroidLogcatLoggerProvider.Tag,
                $"Auth Tab closed without an authentication callback (result {result?.ResultCode}).");
            await handler.AbandonAsync();
        }
        catch (Exception exception)
        {
            AndroidLog.Warn(
                AndroidLogcatLoggerProvider.Tag,
                $"Auth Tab result dispatch failed with {exception.GetType().FullName}.");
        }
    }
}
