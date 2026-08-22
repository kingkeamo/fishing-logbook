using AwesomeAssertions;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Tests.Browser.Update.TestSupport;

namespace FishingLogBook.Web.Tests.Browser.Update.AppUpdateServiceTests;

public class WhenTestingStateChange : BaseAppUpdateServiceTest
{
    [Fact]
    public async Task ItShouldReportAnUpdateThatArrivesAfterStartup()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.NoUpdateJson };
        var sut = CreateService(js);
        await sut.StartAsync(CancellationToken.None);
        var notifications = 0;
        sut.StatusChanged += () => notifications++;

        // Act
        js.Publish(FakeAppUpdateJsRuntime.UpdateReadyJson);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Available);
        notifications.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldNotRaiseAgainForTheSameUpdate()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.NoUpdateJson };
        var sut = CreateService(js);
        await sut.StartAsync(CancellationToken.None);
        var notifications = 0;
        sut.StatusChanged += () => notifications++;

        // Act
        js.Publish(FakeAppUpdateJsRuntime.UpdateReadyJson);
        js.Publish(FakeAppUpdateJsRuntime.UpdateReadyJson);
        js.Publish(FakeAppUpdateJsRuntime.UpdateReadyJson);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Available);
        notifications.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldNotKeepOfferingARetryOnceTheWaitingUpdateHasGone()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime
        {
            StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson,
            ApplyAccepted = false
        };
        var sut = CreateService(js);
        await sut.StartAsync(CancellationToken.None);
        await sut.ApplyAsync(CancellationToken.None);
        sut.Status.Should().Be(AppUpdateStatus.Failed);

        // Act
        js.Publish(FakeAppUpdateJsRuntime.NoUpdateJson);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Current);
    }

    [Fact]
    public async Task ItShouldReturnToCurrentWhenTheWaitingUpdateDisappears()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson };
        var sut = CreateService(js);
        await sut.StartAsync(CancellationToken.None);

        // Act
        js.Publish(FakeAppUpdateJsRuntime.NoUpdateJson);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Current);
    }
}
