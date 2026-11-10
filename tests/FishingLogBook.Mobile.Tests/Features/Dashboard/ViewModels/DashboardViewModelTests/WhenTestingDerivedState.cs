using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardViewModelTests;

public sealed class WhenTestingDerivedState : BaseDashboardViewModelTest
{
    private static readonly Guid BestCatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private void GivenDashboard(DashboardDto dashboard)
    {
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DashboardDto>.Succeeded(dashboard));
    }

    [Fact]
    public void ItShouldShowNothingBeforeTheDashboardHasLoaded()
    {
        // Arrange
        // Act
        var summary = Sut.AppliedFilterSummary;

        // Assert
        Sut.HasData.Should().BeFalse();
        Sut.HasStatus.Should().BeFalse();
        Sut.CatchesTotal.Should().BeEmpty();
        Sut.TripsTotal.Should().BeEmpty();
        Sut.SpeciesTotal.Should().BeEmpty();
        Sut.FilteredCatchesTotal.Should().BeEmpty();
        Sut.FilteredTripsTotal.Should().BeEmpty();
        Sut.FilteredSpeciesTotal.Should().BeEmpty();
        Sut.ShowImportSuggestion.Should().BeFalse();
        Sut.ShowLifetimeEmpty.Should().BeFalse();
        Sut.ShowStatisticsEmpty.Should().BeFalse();
        Sut.ShowFilteredEmpty.Should().BeFalse();
        Sut.HasActiveFilters.Should().BeFalse();
        Sut.LifetimeEmptyTitle.Should().Be(Text["DashboardWeightNeeded"]);
        Sut.StatisticsEmptyText.Should().Be(Text["DashboardNoMatches"]);
        Sut.FilteredEmptyText.Should().Be(Text["DashboardNoMatches"]);
        summary.Should().Contain(Text["DashboardAllSpecies"]).And.Contain(Text["DashboardAllMethods"]);
    }

    [Fact]
    public async Task ItShouldSuggestImportingWhenTheAnglerHasNoCatchesAtAll()
    {
        // Arrange
        GivenDashboard(new DashboardDto());

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.ShowImportSuggestion.Should().BeTrue();
        Sut.ShowLifetimeEmpty.Should().BeTrue();
        Sut.ShowStatisticsEmpty.Should().BeTrue();
        Sut.ShowFilteredEmpty.Should().BeTrue();
        Sut.LifetimeEmptyTitle.Should().Be(Text["DashboardFirstBest"]);
        Sut.StatisticsEmptyText.Should().Be(Text["DashboardNoCatches"]);
        Sut.FilteredEmptyText.Should().Be(Text["DashboardNoCatches"]);
        await MockClient.Received(1).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExplainThatWeightsAreNeededWhenCatchesExistWithoutAnyWeights()
    {
        // Arrange
        GivenDashboard(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 4, Trips = 2, Species = 3 },
            Filtered = new DashboardTotalsDto { Catches = 2, Trips = 1, Species = 1 },
            CatchStatistics = [new DashboardCatchCountDto { SpeciesName = null, Method = null, Count = 4 }]
        });

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.ShowImportSuggestion.Should().BeFalse();
        Sut.ShowLifetimeEmpty.Should().BeTrue();
        Sut.ShowStatisticsEmpty.Should().BeFalse();
        Sut.ShowFilteredEmpty.Should().BeTrue();
        Sut.LifetimeEmptyTitle.Should().Be(Text["DashboardWeightNeeded"]);
        Sut.FilteredEmptyText.Should().Be(Text["DashboardWeightNeeded"]);
        Sut.CatchesTotal.Should().Be("4");
        Sut.TripsTotal.Should().Be("2");
        Sut.SpeciesTotal.Should().Be("3");
        Sut.FilteredCatchesTotal.Should().Be("2");
        Sut.FilteredTripsTotal.Should().Be("1");
        Sut.FilteredSpeciesTotal.Should().Be("1");
        Sut.Statistics.Single().Species.Should().Be(Text["DashboardUnknownSpecies"]);
        Sut.Statistics.Single().Method.Should().Be(Text["DashboardUnknownMethod"]);
    }

    [Fact]
    public async Task ItShouldShowACaughtOnDateOrNotRecordedForEachPersonalBest()
    {
        // Arrange
        GivenDashboard(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 2 },
            AllTimePersonalBests =
            [
                new DashboardPersonalBestDto { CatchId = BestCatchId, Weight = 2, CaughtOn = null },
                new DashboardPersonalBestDto
                {
                    CatchId = Guid.NewGuid(),
                    Weight = 3,
                    SpeciesName = "Pike",
                    Method = "Spinning",
                    CaughtOn = DateTimeOffset.Parse("2026-08-17T09:00Z")
                }
            ]
        });

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.LifetimeBests.Should().HaveCount(2);
        Sut.LifetimeBests[0].CaughtOn.Should().Contain(Text["NotRecorded"]);
        Sut.LifetimeBests[1].CaughtOn.Should().Contain(Text["DashboardCaughtOn"]).And.NotContain(Text["NotRecorded"]);
        Sut.LifetimeBests[1].Species.Should().Be("Pike");
    }

    [Fact]
    public async Task ItShouldDescribeACustomDateRangeAndEveryActiveFilterInTheSummary()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        ChooseFilters(new DashboardFilterSelectionModel
        {
            Preset = DashboardDatePresetEnum.Custom,
            From = new DateTime(2026, 3, 1),
            To = new DateTime(2026, 3, 31),
            Species = "Pike",
            Method = "Spinning",
            Venue = "Lough Corrib"
        });

        // Act
        await Sut.EditFiltersAsync(CancellationToken.None);

        // Assert
        Sut.HasActiveFilters.Should().BeTrue();
        Sut.AppliedFilterSummary.Should()
            .Contain("Pike")
            .And.Contain("Spinning")
            .And.Contain("Lough Corrib")
            .And.Contain("2026");
        await MockClient.Received(1).GetAsync(
            Arg.Is<DashboardFilterDto>(filter => filter.Species == "Pike"
                && filter.Method == "Spinning"
                && filter.Venue == "Lough Corrib"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, "species")]
    [InlineData(true, "method")]
    public async Task ItShouldTreatAnyOneActiveFilterAsActive(bool useMethod, string label)
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        ChooseFilters(new DashboardFilterSelectionModel
        {
            Species = useMethod ? null : "Pike",
            Method = useMethod ? "Fly" : null
        });

        // Act
        await Sut.EditFiltersAsync(CancellationToken.None);

        // Assert
        Sut.HasActiveFilters.Should().BeTrue($"a {label} filter alone is an active filter");
    }

    [Fact]
    public async Task ItShouldClearTheDashboardWhenTheAccountSessionEnds()
    {
        // Arrange
        GivenDashboard(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 1 } });
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        await AccountCancellation.CancelAsync();

        // Assert
        Sut.HasData.Should().BeFalse();
        Sut.StatusText.Should().Be(Text["DashboardOffline"]);
    }

    [Fact]
    public async Task ItShouldDropDataBelongingToAPreviousAccountBeforeLoadingForTheNewOne()
    {
        // Arrange
        GivenDashboard(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 7 } });
        await Sut.LoadAsync(CancellationToken.None);
        var nextLease = new AccountSessionLeaseModel(Guid.NewGuid(), 2, CancellationToken.None);
        MockSession.Capture().Returns(nextLease);
        MockSession.IsCurrent(nextLease).Returns(true);
        MockSession.IsCurrent(Lease).Returns(false);
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 1 } }));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.CatchesTotal.Should().Be("1");
        await MockClient.Received(2).GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheOfflineMessageWhenThereIsNoSessionToLoadFor()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.StatusText.Should().Be(Text["DashboardOffline"]);
        await MockClient.DidNotReceive().GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOpenACatchWithoutASession()
    {
        // Arrange
        GivenDashboard(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 1 },
            AllTimePersonalBests = [new DashboardPersonalBestDto { CatchId = BestCatchId, Weight = 2 }]
        });
        await Sut.LoadAsync(CancellationToken.None);
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        await Sut.OpenCatchAsync(BestCatchId, CancellationToken.None);

        // Assert
        await MockCatches.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOpenACatchThatIsNotOnTheDashboard()
    {
        // Arrange
        GivenDashboard(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 1 },
            AllTimePersonalBests = [new DashboardPersonalBestDto { CatchId = BestCatchId, Weight = 2 }]
        });
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        await Sut.OpenCatchAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await MockCatches.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOpenACatchWhileOffline()
    {
        // Arrange
        GivenDashboard(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 1 },
            AllTimePersonalBests = [new DashboardPersonalBestDto { CatchId = BestCatchId, Weight = 2 }]
        });
        await Sut.LoadAsync(CancellationToken.None);
        MockConnectivity.IsOnline.Returns(false);

        // Act
        await Sut.OpenCatchAsync(BestCatchId, CancellationToken.None);

        // Assert
        await MockCatches.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockNavigator.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<CatchPresentationModel>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheOfflineMessageWhenConnectivityIsLostAfterLoading()
    {
        // Arrange
        GivenDashboard(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 1 } });
        await Sut.LoadAsync(CancellationToken.None);
        MockConnectivity.IsOnline.Returns(false);

        // Act
        MockConnectivity.ConnectivityChanged += Raise.Event<EventHandler>(MockConnectivity, EventArgs.Empty);
        await WaitUntilAsync(() => Sut.StatusText == Text["DashboardOffline"]);

        // Assert
        Sut.HasData.Should().BeFalse();
        Sut.StatusText.Should().Be(Text["DashboardOffline"]);
    }

    [Fact]
    public async Task ItShouldIgnoreAConnectivityChangeWhileStillOnline()
    {
        // Arrange
        GivenDashboard(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 1 } });
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        MockConnectivity.ConnectivityChanged += Raise.Event<EventHandler>(MockConnectivity, EventArgs.Empty);
        await Task.Delay(50);

        // Assert
        Sut.HasData.Should().BeTrue();
        Sut.HasStatus.Should().BeFalse();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }
}
