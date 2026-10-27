using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using FishingLogBook.Mobile.Native.Browsers;
using Microsoft.Extensions.DependencyInjection;
using AndroidLog = Android.Util.Log;

namespace FishingLogBook.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    Exported = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "https",
    DataHost = "fishing-logbook-dev.pages.dev",
    DataPaths = ["/native/oauth2redirect", "/native/signedout"],
    AutoVerify = true)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        MauiAuthenticationBrowser.RegisterAuthTab(this);
        DispatchAuthenticationCallback(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        DispatchAuthenticationCallback(intent);
    }

    private static async void DispatchAuthenticationCallback(Intent? intent)
    {
        try
        {
            var callbackUri = intent?.DataString;
            var handler = IPlatformApplication.Current?.Services.GetService<AuthenticationCallbackHandler>();
            if (handler is not null && handler.CanForward(callbackUri))
            {
                await handler.ForwardAsync(callbackUri);
            }
        }
        catch (Exception exception)
        {
            AndroidLog.Warn(
                AndroidLogcatLoggerProvider.Tag,
                $"Native authentication callback dispatch failed with {exception.GetType().FullName}.");
        }
    }
}
