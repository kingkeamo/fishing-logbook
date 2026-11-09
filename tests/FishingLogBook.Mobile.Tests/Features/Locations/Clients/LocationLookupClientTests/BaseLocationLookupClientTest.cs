using FishingLogBook.Mobile.Features.Locations.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Locations.Clients.LocationLookupClientTests;

public abstract class BaseLocationLookupClientTest : BaseAuthorizedApiClientTest
{
    protected (LocationLookupClient Client, RecordingHttpMessageHandler Server) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var (apiClient, server) = CreateSut(respond);
        return (new LocationLookupClient(apiClient), server);
    }
}
