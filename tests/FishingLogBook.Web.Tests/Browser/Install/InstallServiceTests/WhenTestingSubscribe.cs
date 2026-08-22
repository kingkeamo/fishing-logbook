using AwesomeAssertions;
using FishingLogBook.Web.Browser.Install;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallServiceTests;

public class WhenTestingSubscribe : BaseInstallServiceTest
{
    [Fact]
    public async Task ItShouldRejectAMissingCallback()
    {
        // Arrange
        var js = CreateJsRuntime(IosSafariStateJson);
        var sut = new InstallService(js);

        // Act
        var act = async () => await sut.SubscribeAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldSurfaceTheFailureWhenTheBrowserCannotRegisterTheSubscription()
    {
        // Arrange
        var js = CreateJsRuntime(IosSafariStateJson);
        js.SubscribeFailure = new InvalidOperationException("no subscribe export");
        var sut = new InstallService(js);

        // Act
        var act = async () => await sut.SubscribeAsync(_ => Task.CompletedTask, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().Equal("import", "subscribeInstallState");
    }

    [Fact]
    public async Task ItShouldReleaseTheCallbackTargetWhenDisposed()
    {
        // Arrange
        var js = CreateJsRuntime(IosSafariStateJson);
        var sut = new InstallService(js);
        var received = new List<InstallState>();
        var subscription = await sut.SubscribeAsync(
            state =>
            {
                received.Add(state);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Act
        await subscription.DisposeAsync();
        var act = async () => await js.PublishAsync(InstalledDesktopStateJson);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
        received.Should().BeEmpty();
        js.UnsubscribedTokens.Should().Equal(js.SubscriptionToken);
        js.Invocations.Should().Equal("import", "subscribeInstallState", "unsubscribeInstallState");
    }

    [Fact]
    public async Task ItShouldIgnoreALateStateChangeThatArrivesDuringDisposal()
    {
        // Arrange
        var js = CreateJsRuntime(IosSafariStateJson);
        var sut = new InstallService(js);
        var received = new List<InstallState>();
        var subscription = (InstallStateSubscription)await sut.SubscribeAsync(
            state =>
            {
                received.Add(state);
                return Task.CompletedTask;
            },
            CancellationToken.None);
        await subscription.DisposeAsync();

        // Act
        await subscription.OnInstallStateChanged(
            new InstallState(true, false, InstallPlatformFamilies.Desktop, false));

        // Assert
        received.Should().BeEmpty();
        js.UnsubscribedTokens.Should().Equal(js.SubscriptionToken);
    }

    [Fact]
    public async Task ItShouldOnlyUnsubscribeOnceWhenDisposedRepeatedly()
    {
        // Arrange
        var js = CreateJsRuntime(IosSafariStateJson);
        var sut = new InstallService(js);
        var subscription = await sut.SubscribeAsync(_ => Task.CompletedTask, CancellationToken.None);

        // Act
        await subscription.DisposeAsync();
        await subscription.DisposeAsync();

        // Assert
        js.UnsubscribedTokens.Should().Equal(js.SubscriptionToken);
        js.Invocations.Should().Equal("import", "subscribeInstallState", "unsubscribeInstallState");
    }

    [Fact]
    public async Task ItShouldRouteBrowserStateChangesToTheCallback()
    {
        // Arrange
        var js = CreateJsRuntime(IosSafariStateJson);
        var sut = new InstallService(js);
        var received = new List<InstallState>();

        // Act
        await using var subscription = await sut.SubscribeAsync(
            state =>
            {
                received.Add(state);
                return Task.CompletedTask;
            },
            CancellationToken.None);
        await js.PublishAsync(AndroidPromptableStateJson);
        await js.PublishAsync(InstalledDesktopStateJson);

        // Assert
        received.Should().Equal(
            new InstallState(false, true, InstallPlatformFamilies.Android, false),
            new InstallState(true, false, InstallPlatformFamilies.Desktop, false));
        js.Invocations.Should().Equal("import", "subscribeInstallState");
    }
}
