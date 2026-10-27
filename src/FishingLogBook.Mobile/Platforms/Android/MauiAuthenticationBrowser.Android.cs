using AndroidX.Activity;
using AndroidX.Activity.Result;
using AndroidX.Browser.Auth;
using AndroidX.Browser.CustomTabs;
using FishingLogBook.Mobile.Native.Callbacks;
using AndroidUri = Android.Net.Uri;

namespace FishingLogBook.Mobile.Native.Browsers;

public sealed partial class MauiAuthenticationBrowser
{
    private static ActivityResultLauncher? _authTabLauncher;

    public static void RegisterAuthTab(ComponentActivity activity)
    {
        _authTabLauncher = AuthTabIntent.RegisterActivityResultLauncher(activity, new AuthTabResultCallback());
    }

    private static partial void OpenOnMainThread(Uri uri, Uri callbackUri)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No foreground activity is available for native sign-in.");
        var target = AndroidUri.Parse(uri.AbsoluteUri)
            ?? throw new InvalidOperationException("The native sign-in address is invalid.");
        var browserPackage = CustomTabsClient.GetPackageName(activity, null);
        if (_authTabLauncher is not null &&
            browserPackage is not null &&
            callbackUri.Scheme == Uri.UriSchemeHttps &&
            CustomTabsClient.IsAuthTabSupported(activity, browserPackage))
        {
            var authTab = new AuthTabIntent.Builder().Build()
                ?? throw new InvalidOperationException("The Auth Tab could not be created.");
            authTab.Launch(_authTabLauncher, target, callbackUri.Host, callbackUri.AbsolutePath);
            return;
        }

        var customTab = new CustomTabsIntent.Builder().Build()
            ?? throw new InvalidOperationException("The Custom Tab could not be created.");
        customTab.LaunchUrl(activity, target);
    }
}
