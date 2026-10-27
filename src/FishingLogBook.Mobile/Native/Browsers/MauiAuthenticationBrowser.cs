using FishingLogBook.Mobile.Features.Authentication.Browsers;

namespace FishingLogBook.Mobile.Native.Browsers;

public sealed class MauiAuthenticationBrowser : IAuthenticationBrowser
{
    public async Task OpenAsync(Uri uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var opened = await Launcher.Default.OpenAsync(uri);
        if (!opened)
        {
            throw new InvalidOperationException("The system browser could not be opened.");
        }
    }
}
