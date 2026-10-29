using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Features.Authentication.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.OidcRefreshServiceTests;

public abstract class BaseOidcRefreshServiceTest
{
    protected const string RefreshToken = "refresh-token";
    protected readonly IOidcProtocolClient MockOidcClient = Substitute.For<IOidcProtocolClient>();
    protected readonly TimeProvider MockTimeProvider = Substitute.For<TimeProvider>();
    protected readonly OidcRefreshService Sut;

    protected BaseOidcRefreshServiceTest()
    {
        MockTimeProvider.TimestampFrequency.Returns(TimeSpan.TicksPerSecond);
        MockTimeProvider.GetTimestamp().Returns(0);
        Sut = new OidcRefreshService(MockOidcClient, MockTimeProvider);
    }
}
