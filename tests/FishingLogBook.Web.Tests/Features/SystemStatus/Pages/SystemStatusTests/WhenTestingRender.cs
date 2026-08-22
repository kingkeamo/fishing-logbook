using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using FishingLogBook.Web.Localization;
using NSubstitute;
using SystemStatusPage = FishingLogBook.Web.Features.SystemStatus.Pages.SystemStatus.SystemStatus;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Pages.SystemStatusTests;

public class WhenTestingRender : BaseSystemStatusTest
{
    [Fact]
    public async Task ItShouldShowTheCurrentBrandHeader()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var statusClient = Substitute.For<ISystemStatusClient>();
        await using var context = CreateContext(statusClient);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var header = cut.Find("#system-status-header");
            header.TextContent.Should().Contain("Catch But Don’t Forget");
            header.TextContent.Should().Contain("System status");
            header.TextContent.Should().NotContain("Catch, But Don’t Forget");
            cut.Find("#system-status-brand-mark").GetAttribute("src")
                .Should().Be("images/brand/brand-mark-transparent.png");
            header.QuerySelectorAll(".mud-icon-root").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldShowOnlineWhenApiAndDatabaseAreHealthy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var statusClient = Substitute.For<ISystemStatusClient>();
        statusClient.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new HealthDto("Healthy"));
        statusClient.GetBuildMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new BuildMetadataDto("0.2.0", "api5678", "prod", null));
        statusClient.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new DatabaseTestDto("Healthy", "FishingLogBook database online"));
        await using var context = CreateContext(statusClient);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#status-row-api").TextContent.Should().Contain("Online");
            cut.Find("#status-row-database").TextContent.Should().Contain("Online");
            cut.Find("#status-row-database").TextContent.Should().Contain("FishingLogBook database online");
            cut.Find("#web-build-information").TextContent.Should().Contain("web1234");
            cut.Find("#api-build-information").TextContent.Should().Contain("api5678");
        });
        await statusClient.Received(1).GetApiHealthAsync(Arg.Any<CancellationToken>());
        await statusClient.Received(1).GetBuildMetadataAsync(Arg.Any<CancellationToken>());
        await statusClient.Received(1).GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowOfflineWhenApiIsUnreachable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var statusClient = Substitute.For<ISystemStatusClient>();
        statusClient.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns<HealthDto?>(_ => throw new HttpRequestException("offline"));
        await using var context = CreateContext(statusClient);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#status-row-api").TextContent.Should().Contain("Offline");
            cut.Find("#status-row-database").TextContent.Should().Contain("Offline");
        });
        await statusClient.Received(1).GetApiHealthAsync(Arg.Any<CancellationToken>());
        await statusClient.DidNotReceive().GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepApiHealthVisibleWhenBuildMetadataIsUnavailable()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        var statusClient = Substitute.For<ISystemStatusClient>();
        statusClient.GetApiHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthDto("Healthy"));
        statusClient.GetBuildMetadataAsync(Arg.Any<CancellationToken>())
            .Returns<BuildMetadataDto?>(_ => throw new HttpRequestException("unavailable"));
        statusClient.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new DatabaseTestDto("Healthy", "FishingLogBook database online"));
        await using var context = CreateContext(statusClient);

        var cut = context.Render<SystemStatusPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#status-row-api").TextContent.Should().Contain("Online");
            cut.Find("#api-build-unavailable").TextContent.Should().Contain("Unavailable");
        });
    }

    [Fact]
    public async Task ItShouldShowFrenchCopyWhenUiCultureIsFrench()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var statusClient = Substitute.For<ISystemStatusClient>();
        statusClient.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new HealthDto("Healthy"));
        statusClient.GetBuildMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new BuildMetadataDto("0.2.0", "api5678", "prod", null));
        statusClient.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new DatabaseTestDto("Healthy", "FishingLogBook database online"));
        await using var context = CreateContext(statusClient);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("État du système");
            cut.Find("#status-row-api").TextContent.Should().Contain("En ligne");
            cut.Find("#status-row-database").TextContent.Should().Contain("Base de données");
            cut.Find("#refresh-status-button").TextContent.Should().Contain("Actualiser");
            cut.Find("#build-information").TextContent.Should().Contain("À propos");
            cut.Find("#build-information").TextContent.Should().Contain("Environnement");
        });
        await statusClient.Received(1).GetApiHealthAsync(Arg.Any<CancellationToken>());
        await statusClient.Received(1).GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }
}
