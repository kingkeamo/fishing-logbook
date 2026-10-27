using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Handlers;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Providers;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Handlers.ApiBearerHandlerTests;

public abstract class BaseApiBearerHandlerTest : IDisposable
{
    protected static readonly Guid UserId = Guid.Parse("54a09971-c774-46c7-ab92-ff286f11a414");
    protected readonly IAccessTokenProvider MockAccessTokenProvider = Substitute.For<IAccessTokenProvider>();
    protected readonly IAccountSessionCoordinator MockSessionCoordinator = Substitute.For<IAccountSessionCoordinator>();
    protected readonly AccountSessionLeaseModel Lease = new(UserId, 4, CancellationToken.None);

    protected BaseApiBearerHandlerTest()
    {
        MockAccessTokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>())
            .Returns(new ApiAccessTokenModel("access-token", Lease));
        MockSessionCoordinator.IsCurrent(Lease).Returns(true);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    protected HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        var handler = new ApiBearerHandler(CreateConfig(), MockAccessTokenProvider, MockSessionCoordinator)
        {
            InnerHandler = new RecordingMessageHandler(response)
        };
        return new HttpClient(handler);
    }

    private static MobileAuthConfig CreateConfig()
    {
        return new MobileAuthConfig
        {
            Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_pool",
            ClientId = "client123",
            HostedUiDomain = "https://auth.example.com",
            ApiBaseUrl = "https://api.example.com",
            ApiScope = "https://api.example.com/access",
            ApiResource = "https://api.example.com",
            RedirectUri = "com.fishinglogbook.mobile.demo://oauth2redirect",
            LogoutRedirectUri = "com.fishinglogbook.mobile.demo://signedout"
        };
    }

    private sealed class RecordingMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(response(request));
        }
    }
}
