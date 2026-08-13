using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.SystemStatus;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Pages.SystemStatus;
using FishingLogBook.Web.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.SystemStatusTests;

public class WhenTestingRender : BaseSystemStatusTest
{
    [Fact]
    public async Task ItShouldShowOnline_WhenApiAndDatabaseAreHealthy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var statusClient = Substitute.For<ISystemStatusClient>();
        statusClient.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new HealthResponse("Healthy"));
        statusClient.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new DatabaseTestResponse("Healthy", "FishingLogBook database online"));
        await using var context = CreateContext(statusClient);

        // Act
        var cut = context.Render<SystemStatus>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#status-row-api").TextContent.Should().Contain("Online");
            cut.Find("#status-row-database").TextContent.Should().Contain("Online");
            cut.Find("#status-row-database").TextContent.Should().Contain("FishingLogBook database online");
        });
        await statusClient.Received(1).GetApiHealthAsync(Arg.Any<CancellationToken>());
        await statusClient.Received(1).GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowOffline_WhenApiIsUnreachable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var statusClient = Substitute.For<ISystemStatusClient>();
        statusClient.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns<HealthResponse?>(_ => throw new HttpRequestException("offline"));
        await using var context = CreateContext(statusClient);

        // Act
        var cut = context.Render<SystemStatus>();

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
    public async Task ItShouldShowFrenchCopy_WhenUiCultureIsFrench()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var statusClient = Substitute.For<ISystemStatusClient>();
        statusClient.GetApiHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new HealthResponse("Healthy"));
        statusClient.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new DatabaseTestResponse("Healthy", "FishingLogBook database online"));
        await using var context = CreateContext(statusClient);

        // Act
        var cut = context.Render<SystemStatus>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("État du système");
            cut.Find("#status-row-api").TextContent.Should().Contain("En ligne");
            cut.Find("#status-row-database").TextContent.Should().Contain("Base de données");
            cut.Find("#refresh-status-button").TextContent.Should().Contain("Actualiser");
        });
        await statusClient.Received(1).GetApiHealthAsync(Arg.Any<CancellationToken>());
    }
}
