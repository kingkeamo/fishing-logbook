using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Install.InstallGuidanceTests;

public class WhenTestingStateChange : BaseInstallGuidanceTest
{
    [Fact]
    public async Task ItShouldStillShowManualGuidanceWhenTheSubscriptionCannotBeCreated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var service = CreateService(Android);
        service.SubscribeAsync(Arg.Any<Func<InstallState, Task>>(), Arg.Any<CancellationToken>())
            .Returns<IAsyncDisposable>(_ => throw new InvalidOperationException("subscribe failed"));
        await using var context = CreateContext(service, logging);

        // Act
        var cut = context.Render<InstallGuidance>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-android-steps").Children.Should().HaveCount(4);
            cut.Find("#install-guidance-iphone-steps").Children.Should().HaveCount(8);
            cut.Find("#install-guidance-ipad-steps").Children.Should().HaveCount(6);
            cut.Find("#install-guidance-computer-steps").Children.Should().HaveCount(4);
        });
        await logging.Received(1).LogErrorAsync(
            "install detection",
            Arg.Is<Exception>(exception => exception.Message == "subscribe failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotMoveTheExpandedSectionWhenStateArrivesLater()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var subscribed = CreateSubscribedService(InstallState.Unknown);
        await using var context = CreateContext(subscribed.Service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-android-panel"));

        // Act
        await subscribed.PublishAsync(new InstallState(false, true, InstallPlatformFamilies.Android, false));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-action").Should().NotBeNull();
            IsPanelExpanded(cut, "install-guidance-android-panel").Should().BeFalse();
            IsPanelExpanded(cut, "install-guidance-ios-panel").Should().BeFalse();
            IsPanelExpanded(cut, "install-guidance-computer-panel").Should().BeFalse();
        });
        await subscribed.Service.Received(1).SubscribeAsync(
            Arg.Is<Func<InstallState, Task>>(callback => callback != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferTheNativeInstallButtonWhenTheBrowserPromptArrivesLater()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var subscribed = CreateSubscribedService(Android);
        await using var context = CreateContext(subscribed.Service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.FindAll("#install-guidance-action").Should().BeEmpty());

        // Act
        await subscribed.PublishAsync(new InstallState(false, true, InstallPlatformFamilies.Android, false));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-action").TextContent.Should().Contain("Install app");
            cut.Find("#install-guidance-native").Should().NotBeNull();
        });
        await subscribed.Service.Received(1).GetStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheInstalledStateWhenTheBrowserReportsInstallation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var subscribed = CreateSubscribedService(new InstallState(false, true, InstallPlatformFamilies.Android, false));
        await using var context = CreateContext(subscribed.Service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));

        // Act
        await subscribed.PublishAsync(new InstallState(true, false, InstallPlatformFamilies.Android, false));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("App is installed");
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
            cut.Find("#install-guidance-android-steps").Should().NotBeNull();
        });
        await subscribed.Service.DidNotReceive().PromptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheInstalledStateWhenTheBrowserHasNotConfirmedTheAcceptedInstallYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var subscribed = CreateSubscribedService(
            new InstallState(false, true, InstallPlatformFamilies.Android, false),
            InstallResult.Accepted);
        await using var context = CreateContext(subscribed.Service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));
        await cut.Find("#install-guidance-action").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-installed"));

        // Act
        await subscribed.PublishAsync(Android);

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("App is installed");
            cut.FindAll("#install-guidance-action").Should().BeEmpty();
            cut.FindAll("#install-guidance-benefit").Should().BeEmpty();
        });
        await subscribed.Service.Received(1).PromptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReleaseTheBrowserSubscriptionWhenDisposed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var subscribed = CreateSubscribedService(Android);
        var context = CreateContext(subscribed.Service);
        var cut = context.Render<InstallGuidance>();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-android-panel"));

        // Act
        await context.DisposeAsync();

        // Assert
        await subscribed.Subscription.Received(1).DisposeAsync();
        await subscribed.Service.Received(1).SubscribeAsync(
            Arg.Is<Func<InstallState, Task>>(callback => callback != null),
            Arg.Any<CancellationToken>());
    }

    private static SubscribedInstallService CreateSubscribedService(
        InstallState state,
        InstallResult promptResult = InstallResult.Unavailable)
    {
        var subscribed = new SubscribedInstallService(
            CreateService(state, promptResult),
            Substitute.For<IAsyncDisposable>());
        subscribed.Service
            .SubscribeAsync(Arg.Any<Func<InstallState, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                subscribed.Published = call.Arg<Func<InstallState, Task>>();
                return subscribed.Subscription;
            });
        return subscribed;
    }

    private sealed class SubscribedInstallService
    {
        public SubscribedInstallService(IInstallService service, IAsyncDisposable subscription)
        {
            Service = service;
            Subscription = subscription;
        }

        public IInstallService Service { get; }

        public IAsyncDisposable Subscription { get; }

        public Func<InstallState, Task>? Published { get; set; }

        public Task PublishAsync(InstallState state)
        {
            if (Published is null)
            {
                throw new InvalidOperationException("The component did not subscribe to state changes.");
            }

            return Published(state);
        }
    }
}
