using FishingLogBook.Mobile.Features.Users.Clients;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Users.Clients.CurrentUserClientTests;

public abstract class BaseCurrentUserClientTest
{
    protected static (CurrentUserClient Client, RecordingHttpMessageHandler Server) CreateSut(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var server = new RecordingHttpMessageHandler(respond);
        var httpClient = new HttpClient(server)
        {
            BaseAddress = new Uri("https://api.example.com/")
        };
        return (new CurrentUserClient(httpClient), server);
    }

    protected static (CurrentUserClient Client, RecordingHttpMessageHandler Server) CreateSut(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return CreateSut((request, _) => Task.FromResult(respond(request)));
    }
}
