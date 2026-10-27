namespace FishingLogBook.Mobile.Features.Authentication.Browsers;

public interface IAuthenticationBrowser
{
    Task OpenAsync(Uri uri, Uri callbackUri, CancellationToken cancellationToken);
}
