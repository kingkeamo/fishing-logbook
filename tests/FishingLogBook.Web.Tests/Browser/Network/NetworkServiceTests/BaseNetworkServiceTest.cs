using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using FishingLogBook.Web.Tests.Browser.Network.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Network.NetworkServiceTests;

public class BaseNetworkServiceTest
{
    protected static NetworkService CreateService(
        FakeNetworkJsRuntime jsRuntime,
        ISystemStatusClient? systemStatus = null)
    {
        return new NetworkService(
            jsRuntime,
            systemStatus ?? Substitute.For<ISystemStatusClient>(),
            NullLogger<NetworkService>.Instance);
    }
}
