using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Page = FishingLogBook.Web.Features.Dashboard.Pages.Dashboard.Dashboard;

namespace FishingLogBook.Web.Tests.Features.Dashboard.Pages.DashboardTests;

public class WhenTestingRender : BaseDashboardTest
{
    [Fact]
    public async Task ItShouldKeepEverySectionVisibleWhileLoadingWithoutPretendingTheAccountIsEmpty()
    {
        // Arrange
        var pending = new TaskCompletionSource<DashboardDto>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.Find("#dashboard-loading").Should().NotBeNull();
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("—");
        cut.Find("#dashboard-all-time").Should().NotBeNull();
        cut.Find("#dashboard-explore").Should().NotBeNull();
        cut.FindAll("#dashboard-import").Should().BeEmpty();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive.HasValue && filter.Species == null), Arg.Any<CancellationToken>());
        pending.SetResult(new DashboardDto());
    }

    [Theory]
    [InlineData("en-GB", "Your first personal best awaits", "Already have fishing photos?")]
    [InlineData("fr-FR", "Votre premier record personnel vous attend", "Vous avez déjà des photos de pêche ?")]
    public async Task ItShouldShowTruthfulZerosAndExistingImportAndRecordJourneys(string culture, string best, string import)
    {
        // Arrange
        using var testCulture = TestCulture.Use(culture);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("0"));
        cut.Find("#dashboard-total-trips").TextContent.Should().Be("0");
        cut.Find("#dashboard-total-species").TextContent.Should().Be("0");
        cut.Find("#dashboard-all-time").TextContent.Should().Contain(best);
        cut.Find("#dashboard-import").TextContent.Should().Contain(import);
        cut.Find("#dashboard-import-link").GetAttribute("href").Should().Be("/import");
        cut.Find("#dashboard-record").GetAttribute("href").Should().Be("/catches/record");
        cut.Find("#dashboard-statistics").Should().NotBeNull();
        cut.Find("#dashboard-filtered-table").Should().NotBeNull();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null && filter.Method == null && filter.Venue == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFailureAndRetryWithoutFirstTimeMessaging()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns<Task<DashboardDto>>(_ => throw new HttpRequestException("Unavailable"), _ => Task.FromResult(new DashboardDto()));
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").TextContent.Should().Contain("could not"));
        cut.FindAll("#dashboard-import").Should().BeEmpty();

        // Act
        await cut.Find("#dashboard-retry").ClickAsync(new());

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("0"));
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive.HasValue && filter.ToExclusive.HasValue), Arg.Any<CancellationToken>());
        await MockLogging.Received(1).LogErrorAsync("dashboard load", "HttpRequestException", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAvoidServerCallsWhenOffline()
    {
        // Arrange
        MockNetwork.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").TextContent.Should().Contain("Connect"));
        cut.FindAll("#dashboard-import").Should().BeEmpty();
        await MockClient.DidNotReceive().GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldDistinguishWeightlessCatchesFromNoFilterMatches(bool hasMatches)
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 3, Trips = 2, Species = 1 },
            CatchStatistics = hasMatches ? [new DashboardCatchCountDto { SpeciesName = "Pike", Count = 3 }] : []
        });
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("3"));
        cut.Find("#dashboard-filtered-table").TextContent.Should().Contain(hasMatches ? "weight" : "match");
        cut.FindAll("#dashboard-import").Should().BeEmpty();
        cut.Find("#dashboard-reset").Should().NotBeNull();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLinkActualWinnersAndUsePreferredWeightUnits()
    {
        // Arrange
        var winner = Guid.NewGuid();
        var filtered = Guid.NewGuid();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 20 },
            AllTimePersonalBests = [new DashboardPersonalBestDto { CatchId = winner, Weight = 12.4m, SpeciesName = "Pike", Method = "Lure" }],
            FilteredPersonalBests = [new DashboardPersonalBestDto { CatchId = filtered, Weight = 4.6m, SpeciesName = "Pike", Method = "Lure" }]
        });
        MockPreferences.GetAsync(Arg.Any<CancellationToken>()).Returns(AnglerPreferencesModel.Empty with { WeightUnit = WeightUnitEnum.Lb });
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Page>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-all-time-table a").GetAttribute("href").Should().Be($"/catches/{winner}"));
        cut.Find("#dashboard-filtered-table a").GetAttribute("href").Should().Be($"/catches/{filtered}");
        cut.Find("#dashboard-all-time-table").TextContent.Should().Contain("lb").And.Contain("oz");
        cut.Find("#dashboard-filtered-table tbody tr").Click();
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith($"/catches/{filtered}");
        await MockPreferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPassTheSameCustomDateAndClassificationFiltersAndResetThem()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(new DashboardDto
        {
            Species = ["Pike"],
            Methods = ["Fly"],
            Venues = ["Lake"],
            Lifetime = new DashboardTotalsDto { Catches = 5 }
        });
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("5"));
        cut.Find("#dashboard-date").Change("Custom");
        cut.Find("#dashboard-from").Change("2026-03-01");
        cut.Find("#dashboard-to").Change("2026-03-01");
        cut.Find("#dashboard-species").Change("Pike");
        cut.Find("#dashboard-method").Change("Fly");
        cut.Find("#dashboard-venue").Change("Lake");

        // Act
        await cut.Find("#dashboard-apply").ClickAsync(new());
        await cut.Find("#dashboard-reset").ClickAsync(new());

        // Assert
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == "Pike" && filter.Method == "Fly" && filter.Venue == "Lake"
            && filter.FromInclusive == DateTimeOffset.Parse("2026-02-28T20:00Z") && filter.ToExclusive == DateTimeOffset.Parse("2026-03-01T20:00Z")), Arg.Any<CancellationToken>());
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null && filter.Method == null && filter.Venue == null), Arg.Any<CancellationToken>());
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("5");
    }

    [Fact]
    public async Task ItShouldRejectReversedCustomDatesWithoutCallingTheApiAgain()
    {
        // Arrange
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("0"));
        cut.Find("#dashboard-date").Change("Custom");
        cut.Find("#dashboard-from").Change("2026-03-02");
        cut.Find("#dashboard-to").Change("2026-03-01");

        // Act
        await cut.Find("#dashboard-apply").ClickAsync(new());

        // Assert
        cut.Find("#dashboard-status").TextContent.Should().Contain("date");
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDiscardALateResponseWhenTheAccountChanges()
    {
        // Arrange
        var pending = new TaskCompletionSource<DashboardDto>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        await using var context = CreateContext();
        var cut = context.Render<Page>();

        // Act
        SignOut();
        pending.SetResult(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 99 } });

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("—"));
        cut.FindAll("#dashboard-import").Should().BeEmpty();
        cut.FindAll("#dashboard-loading").Should().BeEmpty();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearLoadedResultsOnConnectivityLoss()
    {
        // Arrange
        await using var context = CreateContext();
        var cut = context.Render<Page>();
        cut.WaitForAssertion(() => cut.Find("#dashboard-total-catches").TextContent.Should().Be("0"));

        // Act
        MockNetwork.ConnectivityChanged += Raise.Event<Action<bool>>(false);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#dashboard-status").TextContent.Should().Contain("Connect"));
        cut.Find("#dashboard-total-catches").TextContent.Should().Be("—");
        cut.FindAll("#dashboard-import").Should().BeEmpty();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }
}
