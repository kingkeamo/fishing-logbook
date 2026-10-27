using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.FishingPreferenceClientTests;

public abstract class BaseFishingPreferenceClientTest : BaseAuthorizedApiClientTest
{
    protected (FishingPreferenceClient Client, RecordingHttpMessageHandler Server) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var (apiClient, server) = CreateSut(respond);
        return (new FishingPreferenceClient(apiClient), server);
    }
}
