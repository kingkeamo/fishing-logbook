using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using SystemStatusPage = FishingLogBook.Web.Features.SystemStatus.Pages.SystemStatus.SystemStatus;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Pages.SystemStatusTests;

public class WhenTestingDatabaseAndUpdateStates : BaseSystemStatusTest
{
    private static ISystemStatusClient HealthyApiWith(DatabaseTestDto? database)
    {
        var client = Substitute.For<ISystemStatusClient>();
        client.GetApiHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthDto("Healthy"));
        client.GetBuildMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new BuildMetadataDto("0.2.0", "api5678", "prod", null));
        client.GetDatabaseStatusAsync(Arg.Any<CancellationToken>()).Returns(database);
        return client;
    }

    [Theory]
    [InlineData("Degraded", "Degraded")]
    [InlineData("Unhealthy", "Offline")]
    [InlineData("", "Offline")]
    public async Task ItShouldMapTheDatabaseStatusToTheMatchingState(string reported, string expected)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = HealthyApiWith(new DatabaseTestDto(reported, "FishingLogBook database"));
        await using var context = CreateContext(client);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#status-row-database").TextContent.Should().Contain(expected));
        await client.Received(1).GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheDatabaseOfflineWhenTheServerReturnsNoDatabaseStatus()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = HealthyApiWith(null);
        await using var context = CreateContext(client);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#status-row-database").TextContent.Should().Contain("Offline"));
        cut.Find("#status-row-api").TextContent.Should().Contain("Online");
    }

    [Fact]
    public async Task ItShouldShowTheDatabaseOfflineWhenTheDatabaseCheckFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = HealthyApiWith(null);
        client.GetDatabaseStatusAsync(Arg.Any<CancellationToken>())
            .Returns<DatabaseTestDto?>(_ => throw new HttpRequestException("down"));
        await using var context = CreateContext(client);

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#status-row-database").TextContent.Should().Contain("Offline"));
        await client.Received(1).GetDatabaseStatusAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AppUpdateStatus.Activating, "Update_ActivatingTitle", false)]
    [InlineData(AppUpdateStatus.Failed, "Update_FailedTitle", true)]
    [InlineData(AppUpdateStatus.Available, "Update_StatusAvailable", true)]
    public async Task ItShouldDescribeTheUpdateStateAndOfferItOnlyWhenItCanBeApplied(
        AppUpdateStatus status,
        string resourceKey,
        bool canUpdate)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = HealthyApiWith(new DatabaseTestDto("Healthy", "ok"));
        var update = CreateUpdateService(status);
        await using var context = CreateContext(client, update);
        var expected = context.Services.GetRequiredService<IStringLocalizer<UiStrings>>()[resourceKey].Value;

        // Act
        var cut = context.Render<SystemStatusPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#update-status").TextContent.Should().Contain(expected));
        cut.FindAll("#update-now-button").Should().HaveCount(canUpdate ? 1 : 0);
    }
}
