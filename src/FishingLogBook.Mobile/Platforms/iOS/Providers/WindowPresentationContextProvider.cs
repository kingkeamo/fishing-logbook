using AuthenticationServices;
using Foundation;
using UIKit;

namespace FishingLogBook.Mobile.Native.Providers;

public sealed class WindowPresentationContextProvider : NSObject, IASWebAuthenticationPresentationContextProviding
{
    public UIWindow GetPresentationAnchor(ASWebAuthenticationSession session)
    {
        return UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow)
            ?? throw new InvalidOperationException("No key window is available for native sign-in.");
    }
}
