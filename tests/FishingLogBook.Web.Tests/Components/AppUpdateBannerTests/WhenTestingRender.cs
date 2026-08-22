using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Components.AppUpdateBanner;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Components.AppUpdateBannerTests;

public class WhenTestingRender : BaseAppUpdateBannerTest
{
    [Fact]
    public async Task ItShouldTakeNoSpaceWhileTheAppIsCurrent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(AppUpdateStatus.Current);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<AppUpdateBanner>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Trim().Should().BeEmpty();
            cut.FindAll("#app-update-banner").Should().BeEmpty();
            cut.FindAll("#app-update-banner-action").Should().BeEmpty();
        });
        await service.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await service.DidNotReceive().ApplyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferTheUpdateWhenANewVersionIsWaiting()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(AppUpdateStatus.Available);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<AppUpdateBanner>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#app-update-banner-title").TextContent
                .Should().Contain("New CBDF version available");
            cut.Find("#app-update-banner-body").TextContent
                .Should().Contain("Get the latest fixes and features.");
            cut.Find("#app-update-banner-action").TextContent.Should().Contain("Update now");
        });
        await service.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferTheUpdateWhileItIsActivating()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(AppUpdateStatus.Activating);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<AppUpdateBanner>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#app-update-banner").Should().NotBeNull();
            cut.FindAll("#app-update-banner-action").Should().BeEmpty();
            cut.Find("#app-update-banner-body").TextContent.Should().Contain("reopen");
        });
    }

    [Fact]
    public async Task ItShouldLeaveTheAppUsableAfterAFailedUpdate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(AppUpdateStatus.Failed);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<AppUpdateBanner>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#app-update-banner-body").TextContent
                .Should().Contain("keep using the app");
            cut.Find("#app-update-banner-action").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldNotMentionBrowserInternals()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var service = CreateService(AppUpdateStatus.Available);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<AppUpdateBanner>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var banner = cut.Find("#app-update-banner").TextContent;
            banner.Should().NotContainAny(
                "service worker",
                "cache",
                "hard refresh",
                "waiting worker",
                "SHA");
        });
    }

    [Fact]
    public async Task ItShouldShowFrenchGuidance()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var service = CreateService(AppUpdateStatus.Available);
        await using var context = CreateContext(service);

        // Act
        var cut = context.Render<AppUpdateBanner>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#app-update-banner-title").TextContent
                .Should().Contain("Nouvelle version de CBDF disponible");
            cut.Find("#app-update-banner-action").TextContent.Should().Contain("Mettre à jour");
        });
    }
}
