using FishingLogBook.Mobile.Features.Authentication.Browsers;

namespace FishingLogBook.Mobile.Native.Browsers;

public sealed partial class MauiAuthenticationBrowser : IAuthenticationBrowser
{
    public Task OpenAsync(Uri uri, Uri callbackUri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MainThread.InvokeOnMainThreadAsync(() => OpenOnMainThread(uri, callbackUri));
    }

    private static partial void OpenOnMainThread(Uri uri, Uri callbackUri);
}
