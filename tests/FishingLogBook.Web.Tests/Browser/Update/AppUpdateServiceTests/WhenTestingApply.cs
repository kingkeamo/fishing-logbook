using AwesomeAssertions;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Tests.Browser.Update.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Update.AppUpdateServiceTests;

public class WhenTestingApply : BaseAppUpdateServiceTest
{
    [Fact]
    public async Task ItShouldDoNothingWhenNoUpdateIsWaiting()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.NoUpdateJson };
        var sut = CreateService(js);
        await sut.StartAsync(CancellationToken.None);

        // Act
        await sut.ApplyAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Current);
        js.Invocations.Should().NotContain("applyUpdate");
    }

    [Fact]
    public async Task ItShouldStayUsableWhenActivationIsRefused()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime
        {
            StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson,
            ApplyAccepted = false
        };
        var sut = CreateService(js);
        await sut.StartAsync(CancellationToken.None);

        // Act
        await sut.ApplyAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Failed);
        js.Invocations.Should().Contain("applyUpdate");
    }

    [Fact]
    public async Task ItShouldStayUsableWhenActivationThrows()
    {
        // Arrange
        var logging = Substitute.For<ILoggingService>();
        var js = new FakeAppUpdateJsRuntime
        {
            StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson,
            ApplyFailure = new InvalidOperationException("worker is gone")
        };
        var sut = CreateService(js, logging);
        await sut.StartAsync(CancellationToken.None);

        // Act
        await sut.ApplyAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Failed);
        await logging.Received(1).LogErrorAsync(
            "app update",
            Arg.Is<Exception>(exception => exception.Message == "worker is gone"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequestActivationOnlyOnceWhileActivating()
    {
        // Arrange
        var js = new FakeAppUpdateJsRuntime { StateJson = FakeAppUpdateJsRuntime.UpdateReadyJson };
        var sut = CreateService(js);
        await sut.StartAsync(CancellationToken.None);

        // Act
        await sut.ApplyAsync(CancellationToken.None);
        await sut.ApplyAsync(CancellationToken.None);
        await sut.ApplyAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Activating);
        js.Invocations.Count(invocation => invocation == "applyUpdate").Should().Be(1);
    }

    [Fact]
    public async Task ItShouldAllowARetryAfterAFailedActivation()
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
        js.ApplyAccepted = true;

        // Act
        await sut.ApplyAsync(CancellationToken.None);

        // Assert
        sut.Status.Should().Be(AppUpdateStatus.Activating);
        js.Invocations.Count(invocation => invocation == "applyUpdate").Should().Be(2);
    }
}
