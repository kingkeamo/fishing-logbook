using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Layouts.MainLayout;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class WhenTestingUpdateBanner : BaseMainLayoutTest
{
    [Fact]
    public async Task ItShouldNotShowTheUpdateBannerToAnUnauthenticatedVisitor()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var appUpdate = CreateUpdateService(AppUpdateStatus.Available);
        await using var context = CreateContext(isAuthenticated: false, appUpdate: appUpdate);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.FindAll("#app-update-banner").Should().BeEmpty();
        cut.FindAll("#app-update-banner-action").Should().BeEmpty();
        await appUpdate.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTakeNoLayoutSpaceWhileTheAppIsCurrent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var appUpdate = CreateUpdateService(AppUpdateStatus.Current);
        await using var context = CreateContext(isAuthenticated: true, appUpdate: appUpdate);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#app-update-banner").Should().BeEmpty();
            cut.Find("#app-shell-content").Should().NotBeNull();
        });
        await appUpdate.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheUpdateBannerAboveThePageContentForASignedInAngler()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var appUpdate = CreateUpdateService(AppUpdateStatus.Available);
        await using var context = CreateContext(isAuthenticated: true, appUpdate: appUpdate);

        // Act
        var cut = context.Render<MainLayout>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#app-update-banner-title").TextContent
                .Should().Contain("New CBDF version available");
            cut.Find("#app-update-banner-action").TextContent.Should().Contain("Update now");
            cut.Markup.IndexOf("app-update-banner", StringComparison.Ordinal)
                .Should().BeLessThan(cut.Markup.IndexOf("app-shell-content", StringComparison.Ordinal));
        });
        await appUpdate.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    private static IAppUpdateService CreateUpdateService(AppUpdateStatus status)
    {
        var service = Substitute.For<IAppUpdateService>();
        service.Status.Returns(status);
        return service;
    }
}
