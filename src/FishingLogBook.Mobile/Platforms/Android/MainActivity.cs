using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    Exported = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "com.fishinglogbook.mobile.demo",
    DataHost = "oauth2redirect")]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "com.fishinglogbook.mobile.demo",
    DataHost = "signedout")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
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
        if (!Uri.TryCreate(intent?.DataString, UriKind.Absolute, out var callback))
        {
            return;
        }

        var services = IPlatformApplication.Current?.Services;
        var handler = services?.GetService<AuthenticationCallbackHandler>();
        if (handler is not null)
        {
            await handler.HandleAsync(callback);
        }
    }
}
