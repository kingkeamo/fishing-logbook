using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.ProfileClientTests;

public abstract class BaseProfileClientTest : BaseAuthorizedApiClientTest
{
    protected (ProfileClient Client, RecordingHttpMessageHandler Server) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var (apiClient, server) = CreateSut(respond);
        return (new ProfileClient(apiClient), server);
    }
}
