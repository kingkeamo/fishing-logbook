using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardViewModelTests;

public class WhenTestingLoad : BaseDashboardViewModelTest
{
    [Fact]
    public async Task ItShouldAvoidTheApiOfflineAndKeepRecordingNavigationAvailable()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.HasData.Should().BeFalse();
        Sut.StatusText.Should().Contain("Connect");
        Sut.ShowImportSuggestion.Should().BeFalse();
        await MockClient.DidNotReceive().GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDistinguishApiFailureFromAnEmptyAccount()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DashboardDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.HasData.Should().BeFalse();
        Sut.HasStatus.Should().BeTrue();
        Sut.ShowImportSuggestion.Should().BeFalse();
        Sut.CatchesTotal.Should().BeEmpty();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive.HasValue), Arg.Any<CancellationToken>());
        await MockUnits.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepLoadingDistinctFromGenuineZerosAndShowTheFirstTimeActionsAfterSuccess()
    {
        // Arrange
        var pending = new TaskCompletionSource<ApiResultModel<DashboardDto>>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(pending.Task);

        // Act
        var load = Sut.LoadAsync(CancellationToken.None);
        Sut.IsLoading.Should().BeTrue();
        Sut.CatchesTotal.Should().BeEmpty();
        Sut.ShowImportSuggestion.Should().BeFalse();
        pending.SetResult(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto()));
        await load;

        // Assert
        Sut.IsLoading.Should().BeFalse();
        Sut.CatchesTotal.Should().Be("0");
        Sut.TripsTotal.Should().Be("0");
        Sut.SpeciesTotal.Should().Be("0");
        Sut.ShowImportSuggestion.Should().BeTrue();
        Sut.ShowLifetimeEmpty.Should().BeTrue();
        Sut.ShowStatisticsEmpty.Should().BeTrue();
        Sut.ShowFilteredEmpty.Should().BeTrue();
        Sut.LifetimeEmptyTitle.Should().Be("Your first personal best awaits");
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter =>
            filter.FromInclusive == DateTimeOffset.Parse("2026-01-01T05:00Z")
            && filter.ToExclusive == DateTimeOffset.Parse("2027-01-01T05:00Z")), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldShowContextualEmptyMessagesForWeightlessCatchesAndUnmatchedFilters(bool matches)
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 2, Trips = 3 },
            CatchStatistics = matches ? [new DashboardCatchCountDto { Count = 2 }] : []
        }));

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.CatchesTotal.Should().Be("2");
        Sut.TripsTotal.Should().Be("3");
        Sut.ShowImportSuggestion.Should().BeFalse();
        Sut.FilteredEmptyText.Should().Contain(matches ? "weight" : "match");
        Sut.LifetimeBests.Should().BeEmpty();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldConvertInclusiveCustomDaysAcrossDaylightSavingAndApplyAllClassifications()
    {
        // Arrange
        Sut.SelectPreset((int)DashboardDatePresetEnum.Custom);
        Sut.FromDate = Sut.ToDate = new DateTime(2026, 3, 8);
        Sut.SelectedSpecies = new DashboardChoiceModel("Pike", "Pike");
        Sut.SelectedMethod = new DashboardChoiceModel("Fly", "Fly");
        Sut.SelectedVenue = new DashboardChoiceModel("Lake", "Lake");

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter =>
            filter.FromInclusive == DateTimeOffset.Parse("2026-03-08T05:00Z")
            && filter.ToExclusive == DateTimeOffset.Parse("2026-03-09T04:00Z")
            && filter.Species == "Pike" && filter.Method == "Fly" && filter.Venue == "Lake"), Arg.Any<CancellationToken>());
        Sut.HasData.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRejectReversedCustomDatesWithoutRequestingData()
    {
        // Arrange
        Sut.SelectPreset((int)DashboardDatePresetEnum.Custom);
        Sut.FromDate = new DateTime(2026, 3, 9);
        Sut.ToDate = new DateTime(2026, 3, 8);

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.StatusText.Should().Contain("date");
        await MockClient.DidNotReceive().GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearDataOnAccountExitAndIgnoreALateResponse()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var pending = new TaskCompletionSource<ApiResultModel<DashboardDto>>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var load = Sut.LoadAsync(CancellationToken.None);

        // Act
        MockSession.IsCurrent(Lease).Returns(false);
        AccountCancellation.Cancel();
        pending.SetResult(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 99 } }));
        await load;

        // Assert
        Sut.Data.Should().BeNull();
        Sut.LifetimeBests.Should().BeEmpty();
        Sut.IsLoading.Should().BeFalse();
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetainTheLatestResultWhenResponsesArriveOutOfOrder()
    {
        // Arrange
        var pending = new TaskCompletionSource<ApiResultModel<DashboardDto>>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task, Task.FromResult(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 2 } })));
        var first = Sut.LoadAsync(CancellationToken.None);

        // Act
        await Sut.LoadAsync(CancellationToken.None);
        pending.SetResult(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 1 } }));
        await first;

        // Assert
        Sut.CatchesTotal.Should().Be("2");
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUsePreferredUnitsAndRefreshFrenchLabelsWithoutChangingUserData()
    {
        // Arrange
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 1 },
            AllTimePersonalBests = [new DashboardPersonalBestDto { CatchId = Guid.NewGuid(), Weight = 4.6m, SpeciesName = "Pike" }]
        }));

        // Act
        await Sut.LoadAsync(CancellationToken.None);
        Text.SetCulture("fr-FR");

        // Assert
        Sut.LifetimeBests.Should().ContainSingle(row => row.Species == "Pike" && row.Value.Contains("lb"));
        Sut.LifetimeBests[0].Method.Should().Be(Text["DashboardUnknownMethod"]);
        Sut.DatePresets.Should().Contain("Cette année");
        await MockUnits.Received(1).GetAsync(Arg.Any<CancellationToken>());
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearLoadedResultsWhenConnectivityIsLost()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        MockConnectivity.IsOnline.Returns(false);
        MockConnectivity.ConnectivityChanged += Raise.Event<EventHandler>(MockConnectivity, EventArgs.Empty);

        // Assert
        Sut.HasData.Should().BeFalse();
        Sut.StatusText.Should().Contain("Connect");
        Sut.ShowImportSuggestion.Should().BeFalse();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResetAllSharedFiltersToThisYear()
    {
        // Arrange
        Sut.SelectPreset((int)DashboardDatePresetEnum.AllTime);
        Sut.SelectedSpecies = new DashboardChoiceModel("Pike", "Pike");
        Sut.SelectedMethod = new DashboardChoiceModel("Fly", "Fly");
        Sut.SelectedVenue = new DashboardChoiceModel("Lake", "Lake");

        // Act
        await Sut.ResetAsync(CancellationToken.None);

        // Assert
        Sut.PresetIndex.Should().Be((int)DashboardDatePresetEnum.ThisYear);
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null
            && filter.Method == null && filter.Venue == null
            && filter.FromInclusive == DateTimeOffset.Parse("2026-01-01T05:00Z")
            && filter.ToExclusive == DateTimeOffset.Parse("2027-01-01T05:00Z")), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("America/New_York", "2026-11-01", "2026-11-01T04:00Z", "2026-11-02T05:00Z")]
    [InlineData("America/Sao_Paulo", "2018-11-04", "2018-11-04T03:00Z", "2018-11-05T02:00Z")]
    [InlineData("America/Havana", "2015-11-01", "2015-11-01T04:00Z", "2015-11-02T05:00Z")]
    public async Task ItShouldIncludeWholeLocalDaysWhenMidnightIsSkippedOrRepeated(string zone, string date, string start, string end)
    {
        // Arrange
        using var sut = CreateViewModel(zone);
        sut.SelectPreset((int)DashboardDatePresetEnum.Custom);
        sut.FromDate = sut.ToDate = DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        await sut.LoadAsync(CancellationToken.None);

        // Assert
        sut.HasData.Should().BeTrue();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter =>
            filter.FromInclusive == DateTimeOffset.Parse(start) && filter.ToExclusive == DateTimeOffset.Parse(end)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFormatMetricWeightsInTheSelectedFrenchCultureWithoutChangingCanonicalValues()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 1 },
            AllTimePersonalBests = [new DashboardPersonalBestDto { CatchId = Guid.NewGuid(), Weight = 4.6m }]
        }));
        Text.SetCulture("fr-FR");

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.LifetimeBests.Should().ContainSingle(row => row.Value == "4,6 kg");
        Sut.Data!.AllTimePersonalBests[0].Weight.Should().Be(4.6m);
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }
}
