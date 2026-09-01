using AwesomeAssertions;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using FishingLogBook.Web.Tests.Browser.Network.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Network.NetworkServiceTests;

public class WhenTestingIsOnline : BaseNetworkServiceTest
{
    [Fact]
    public async Task ItShouldBeOfflineImmediatelyWhenTheBrowserIsOffline()
    {
        // Arrange
        var jsRuntime = new FakeNetworkJsRuntime { BrowserOnline = false };
        var systemStatus = Substitute.For<ISystemStatusClient>();
        var sut = CreateService(jsRuntime, systemStatus);

        // Act
        var online = await sut.IsOnlineAsync(CancellationToken.None);

        // Assert
        online.Should().BeFalse();
        await systemStatus.DidNotReceive().IsApiReachableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBeOnlineWhenTheBrowserIsOnlineAndTheApiResponds()
    {
        // Arrange
        var jsRuntime = new FakeNetworkJsRuntime { BrowserOnline = true };
        var systemStatus = Substitute.For<ISystemStatusClient>();
        systemStatus.IsApiReachableAsync(Arg.Any<CancellationToken>()).Returns(true);
        var sut = CreateService(jsRuntime, systemStatus);

        // Act
        var online = await sut.IsOnlineAsync(CancellationToken.None);

        // Assert
        online.Should().BeTrue();
        await systemStatus.Received(1).IsApiReachableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBeOfflineWhenTheBrowserIsOnlineButTheApiIsUnreachable()
    {
        // Arrange
        var jsRuntime = new FakeNetworkJsRuntime { BrowserOnline = true };
        var systemStatus = Substitute.For<ISystemStatusClient>();
        systemStatus.IsApiReachableAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateService(jsRuntime, systemStatus);

        // Act
        var online = await sut.IsOnlineAsync(CancellationToken.None);

        // Assert
        online.Should().BeFalse();
        await systemStatus.Received(1).IsApiReachableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRethrowWhenTheCallerCancelsTheHealthProbe()
    {
        // Arrange
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var jsRuntime = new FakeNetworkJsRuntime { BrowserOnline = true };
        var systemStatus = Substitute.For<ISystemStatusClient>();
        systemStatus.IsApiReachableAsync(Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new OperationCanceledException(cancelled.Token));
        var sut = CreateService(jsRuntime, systemStatus);

        // Act
        var act = () => sut.IsOnlineAsync(cancelled.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldPublishOfflineImmediatelyWhenTheBrowserGoesOffline()
    {
        // Arrange
        var systemStatus = Substitute.For<ISystemStatusClient>();
        var sut = CreateService(new FakeNetworkJsRuntime(), systemStatus);
        bool? published = null;
        sut.ConnectivityChanged += isOnline => published = isOnline;

        // Act
        sut.OnBrowserConnectivityChanged(false);

        // Assert
        published.Should().BeFalse();
        await systemStatus.DidNotReceive().IsApiReachableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPublishAfterVerifyingTheApiWhenTheBrowserComesOnline()
    {
        // Arrange
        var jsRuntime = new FakeNetworkJsRuntime { BrowserOnline = true };
        var systemStatus = Substitute.For<ISystemStatusClient>();
        systemStatus.IsApiReachableAsync(Arg.Any<CancellationToken>()).Returns(true);
        var sut = CreateService(jsRuntime, systemStatus);
        bool? published = null;
        sut.ConnectivityChanged += isOnline => published = isOnline;

        // Act
        sut.OnBrowserConnectivityChanged(true);
        await WaitForAsync(() => published is not null);

        // Assert
        published.Should().BeTrue();
        await systemStatus.Received(1).IsApiReachableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReverifyReachabilityWhenThePageBecomesUsable()
    {
        // Arrange
        var jsRuntime = new FakeNetworkJsRuntime { BrowserOnline = true };
        var systemStatus = Substitute.For<ISystemStatusClient>();
        systemStatus.IsApiReachableAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateService(jsRuntime, systemStatus);
        bool? published = null;
        sut.ConnectivityChanged += isOnline => published = isOnline;

        // Act
        sut.OnBrowserUsable();
        await WaitForAsync(() => published is not null);

        // Assert
        published.Should().BeFalse();
        await systemStatus.Received(1).IsApiReachableAsync(Arg.Any<CancellationToken>());
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var expiry = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < expiry)
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }
}
