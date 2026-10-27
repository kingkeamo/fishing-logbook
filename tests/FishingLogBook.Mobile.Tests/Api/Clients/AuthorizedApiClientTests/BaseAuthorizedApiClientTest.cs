using System.Net;
using System.Text;
using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Providers;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;

public abstract class BaseAuthorizedApiClientTest
{
    protected const string AccessToken = "access-token-value";
    protected static readonly Guid UserId = Guid.Parse("0f8d1c39-5b7f-4f64-9a52-3c4a1d0b6e21");
    protected readonly IAccessTokenProvider MockAccessTokenProvider = Substitute.For<IAccessTokenProvider>();
    protected readonly IAccountSessionCoordinator MockSessionCoordinator = Substitute.For<IAccountSessionCoordinator>();
    protected readonly CapturingLogger<AuthorizedApiClient> Logger = new();
    protected readonly AccountSessionLeaseModel Lease = new(UserId, 1, CancellationToken.None);

    protected BaseAuthorizedApiClientTest()
    {
        MockAccessTokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>())
            .Returns(new ApiAccessTokenModel(AccessToken, Lease));
        MockSessionCoordinator.Capture().Returns(Lease);
        MockSessionCoordinator.IsCurrent(Lease).Returns(true);
    }

    protected (AuthorizedApiClient Client, RecordingHttpMessageHandler Server) CreateSut(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var server = new RecordingHttpMessageHandler(respond);
        var bearer = new ApiBearerHandler(TestMobileAuthConfig.Create(), MockAccessTokenProvider, MockSessionCoordinator)
        {
            InnerHandler = server
        };
        var httpClient = new HttpClient(bearer) { BaseAddress = new Uri(TestMobileAuthConfig.ApiBaseUrl + "/") };
        return (new AuthorizedApiClient(httpClient, MockSessionCoordinator, Logger), server);
    }

    protected (AuthorizedApiClient Client, RecordingHttpMessageHandler Server) CreateSut(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return CreateSut((request, _) => Task.FromResult(respond(request)));
    }

    protected static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    public sealed record SampleDto(string Name);
}
