using Foundation;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using Microsoft.Extensions.DependencyInjection;
using UIKit;

namespace FishingLogBook.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    public override bool OpenUrl(
        UIApplication application,
        NSUrl url,
        NSDictionary options)
    {
        if (Uri.TryCreate(url.AbsoluteString, UriKind.Absolute, out var callback))
        {
            var handler = IPlatformApplication.Current?.Services.GetService<AuthenticationCallbackHandler>();
            if (handler is not null)
            {
                _ = handler.HandleAsync(callback);
                return true;
            }
        }

        return base.OpenUrl(application, url, options);
    }
}
