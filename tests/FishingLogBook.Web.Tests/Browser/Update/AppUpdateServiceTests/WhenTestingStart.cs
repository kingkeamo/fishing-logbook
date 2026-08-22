using AwesomeAssertions;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Tests.Browser.Update.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Update.AppUpdateServiceTests;

public class WhenTestingStart : BaseAppUpdateServiceTest
{
    [Fact]
    public async Task ItShouldStayUsableWhenTheBrowserModuleFails()
    {
        // Arrange
        var logging = Substitute.For<ILoggingService>();
        var js = new FakeAppUpdateJsRuntime
        {
            StateFailure = new InvalidOperationException("module unavailable")
        };
        var sut = CreateService(js, logging);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Current);
        await logging.Received(1).LogErrorAsync(
            "app update",
            Arg.Is<Exception>(exception => exception.Message == "module unavailable"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNoUpdateWhenNothingIsWaiting()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.NoUpdateJson };
        var sut = CreateService(js);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Current);
        js.ImportedModules.Should().Equal(ModulePath);
        js.Invocations.Should().Equal("import", "subscribeUpdateState", "getUpdateState");
    }

    [Fact]
    public async Task ItShouldReportAnUpdateThatIsAlreadyWaiting()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson };
        var sut = CreateService(js);
        var notifications = 0;
        sut.StatusChanged += () => notifications++;

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Available);
        notifications.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldOnlySubscribeOnceAcrossRepeatedStarts()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson };
        var sut = CreateService(js);

        // Act
        await sut.StartAsync(CancellationToken.None);
        await sut.StartAsync(CancellationToken.None);
        await sut.StartAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Available);
        js.Invocations.Should().Equal("import", "subscribeUpdateState", "getUpdateState");
    }

    [Fact]
    public async Task ItShouldReleaseTheBrowserSubscriptionWhenDisposed()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime();
        var sut = CreateService(js);
        await sut.StartAsync(CancellationToken.None);

        // Act
        await sut.DisposeAsync();

        // Assert
        js.UnsubscribedTokens.Should().Equal(js.SubscriptionToken);
    }
}
