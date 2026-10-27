namespace FishingLogBook.Mobile.Features.Authentication.Factories;

public static class NoRedirectHttpMessageHandlerFactory
{
    public static HttpClientHandler Create()
    {
        return new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
    }
}
