using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Components.AppUpdateBanner;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using FishingLogBook.Web.Localization;
using NSubstitute;
using SystemStatusPage = FishingLogBook.Web.Features.SystemStatus.Pages.SystemStatus.SystemStatus;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Pages.SystemStatusTests;

public class WhenTestingUpdateStatus : BaseSystemStatusTest
{
    [Fact]
    public async Task ItShouldReportBeingUpToDateWithoutOfferingAnUpdate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var appUpdate = CreateUpdateService(AppUpdateStatus.Current);
        await using var context = CreateContext(CreateStatusClient(), appUpdate);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#update-status").TextContent.Should().Contain("Up to date");
            cut.FindAll("#update-now-button").Should().BeEmpty();
        });
        await appUpdate.DidNotReceive().ApplyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferTheUpdateWhenANewVersionIsWaiting()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var appUpdate = CreateUpdateService(AppUpdateStatus.Available);
        await using var context = CreateContext(CreateStatusClient(), appUpdate);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#update-status").TextContent.Should().Contain("New version available");
            cut.Find("#update-now-button").TextContent.Should().Contain("Update now");
        });
    }

    [Fact]
    public async Task ItShouldPreserveTheExistingBuildInformation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(CreateStatusClient());

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#web-build-information").TextContent
                .Should().Contain("0.1.0").And.Contain("web1234").And.Contain("prod");
            cut.Find("#update-information").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldAskTheSharedServiceToUpdate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var appUpdate = CreateUpdateService(AppUpdateStatus.Available);
        await using var context = CreateContext(CreateStatusClient(), appUpdate);
        var cut = context.Render<SystemStatusPage>();
        cut.WaitForAssertion(() => cut.Find("#update-now-button"));

        // Act
        await cut.Find("#update-now-button").ClickAsync();

        // Assert
        await appUpdate.Received(1).ApplyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadTheSameUpdateStateAsTheGlobalBanner()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var appUpdate = CreateUpdateService(AppUpdateStatus.Available);
        await using var context = CreateContext(CreateStatusClient(), appUpdate);
        var page = context.Render<SystemStatusPage>();
        var banner = context.Render<AppUpdateBanner>();
        page.WaitForAssertion(() => page.Find("#update-now-button"));
        banner.WaitForAssertion(() => banner.Find("#app-update-banner-action"));

        // Act
        await banner.Find("#app-update-banner-action").ClickAsync();
        await page.Find("#update-now-button").ClickAsync();

        // Assert
        await appUpdate.Received(2).ApplyAsync(Arg.Any<CancellationToken>());
        context.Services.GetService(typeof(IAppUpdateService)).Should().BeSameAs(appUpdate);
    }

    private static ISystemStatusClient CreateStatusClient()
    {
        var client = Substitute.For<ISystemStatusClient>();
        client.GetApiHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthDto("Healthy"));
        client.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new DatabaseTestDto("Healthy", "flb"));
        client.GetBuildMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new BuildMetadataDto("2.0.0", "api9999", "prod", null));
        return client;
    }
}
