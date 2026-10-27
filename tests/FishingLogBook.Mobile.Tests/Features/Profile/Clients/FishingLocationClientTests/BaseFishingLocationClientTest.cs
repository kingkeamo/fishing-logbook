using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.FishingLocationClientTests;

public abstract class BaseFishingLocationClientTest : BaseAuthorizedApiClientTest
{
    protected (FishingLocationClient Client, RecordingHttpMessageHandler Server) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var (apiClient, server) = CreateSut(respond);
        return (new FishingLocationClient(apiClient), server);
    }
}
