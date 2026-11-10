using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using Page = FishingLogBook.Web.Features.Dashboard.Pages.Dashboard.Dashboard;

namespace FishingLogBook.Web.Tests.Features.Dashboard.Pages.DashboardTests;

public class WhenTestingLoadVariations : BaseDashboardTest
{
    [Fact]
    public async Task ItShouldShowTheOfflineMessageWithoutRequestingDataWhenTheDeviceIsOffline()
    {
        // Arrange
        MockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").Should().NotBeNull());
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("—");
        await MockNetwork.Received(1).IsOnlineAsync(Arg.Any<CancellationToken>());
        await MockClient.DidNotReceive().GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAnErrorAndLogItWhenTheDashboardCannotBeLoaded()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DashboardDto>(new HttpRequestException("private detail")));
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").Should().NotBeNull());
        await MockLogging.Received(1).LogErrorAsync(
            "dashboard load",
            nameof(HttpRequestException),
            CancellationToken.None);
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadWhenRetryIsPressedAfterAFailure()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<DashboardDto>(new HttpRequestException("down")),
                _ => Task.FromResult(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 3 } }));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-retry"));

        // Act
        await cut.Find("#dashboard-retry").ClickAsync(new());

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("3"));
        cut.FindAll("#dashboard-status").Should().BeEmpty();
        await MockClient.Received(2).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportInvalidDatesWhenALocalMidnightCannotBeResolved()
    {
        // Arrange
        MockTime.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").TextContent.Should().Contain("date"));
        await MockClient.DidNotReceive().GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendTheChosenSpeciesMethodAndVenueWhenApplied()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(new DashboardDto
        {
            Species = ["Pike"],
            Methods = ["Fly"],
            Venues = ["Lake"],
            Lifetime = new DashboardTotalsDto { Catches = 2 }
        });
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("2"));
        cut.Find("#dashboard-species").Change("Pike");
        cut.Find("#dashboard-method").Change("Fly");
        cut.Find("#dashboard-venue").Change("Lake");
        cut.Find("#dashboard-date").Change("AllTime");

        // Act
        await cut.Find("#dashboard-apply").ClickAsync(new());

        // Assert
        await MockClient.Received(1).GetAsync(
            Arg.Is<DashboardFilterDto>(filter => filter.Species == "Pike"
                && filter.Method == "Fly"
                && filter.Venue == "Lake"
                && filter.FromInclusive == null
                && filter.ToExclusive == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSuggestImportingWhenThereAreNoCatchesYet()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#dashboard-import").Should().NotBeNull();
            cut.Find("#dashboard-counts-empty").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldExplainThatWeightsAreNeededWhenCatchesExistWithoutStatistics()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 4 } });
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("4"));
        cut.FindAll("#dashboard-import").Should().BeEmpty();
    }
}
