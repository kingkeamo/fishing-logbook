using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardViewModelTests;

public class WhenTestingEditFilters : BaseDashboardViewModelTest
{
    [Fact]
    public async Task ItShouldDiscardCancelledEditsWithoutReloadingTheDashboard()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 20 },
            Species = ["Pike"],
            Methods = ["Fly"],
            Venues = ["Lake"]
        }));
        await Sut.LoadAsync(CancellationToken.None);
        var original = Sut.Data;
        var summary = Sut.AppliedFilterSummary;

        // Act
        await Sut.EditFiltersAsync(CancellationToken.None);

        // Assert
        Sut.Data.Should().BeSameAs(original);
        Sut.AppliedFilterSummary.Should().Be(summary);
        Sut.IsFilterModalOpen.Should().BeFalse();
        Sut.CanApply.Should().BeTrue();
        await MockModals.Received(1).EditFiltersAsync(Arg.Is<DashboardFilterSelectionModel>(selection => selection.Preset == DashboardDatePresetEnum.ThisYear),
            Arg.Is<DashboardFilterOptionsModel>(options => options.Species.SequenceEqual(new[] { "Pike" }) && options.Methods.SequenceEqual(new[] { "Fly" }) && options.Venues.SequenceEqual(new[] { "Lake" })), Arg.Any<CancellationToken>());
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepDraftsOutOfTheDashboardAndPreventDuplicateModals()
    {
        // Arrange
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 20, Trips = 6, Species = 4 },
            Filtered = new DashboardTotalsDto { Catches = 3, Trips = 1, Species = 2 }
        }));
        await Sut.LoadAsync(CancellationToken.None);
        var summary = Sut.AppliedFilterSummary;
        var pending = new TaskCompletionSource<DashboardFilterSelectionModel?>();
        MockModals.EditFiltersAsync(Arg.Any<DashboardFilterSelectionModel>(), Arg.Any<DashboardFilterOptionsModel>(), Arg.Any<CancellationToken>()).Returns(pending.Task);

        // Act
        var editing = Sut.EditFiltersAsync(CancellationToken.None);
        var wasOpen = Sut.IsFilterModalOpen;
        await Sut.EditFiltersAsync(CancellationToken.None);
        Sut.AppliedFilterSummary.Should().Be(summary);
        pending.SetResult(new DashboardFilterSelectionModel { Preset = DashboardDatePresetEnum.AllTime, Species = "Pike", Method = "Fly", Venue = "Lake" });
        await editing;

        // Assert
        wasOpen.Should().BeTrue();
        Sut.IsFilterModalOpen.Should().BeFalse();
        Sut.HasActiveFilters.Should().BeTrue();
        Sut.AppliedFilterSummary.Should().Be("All time · Pike · Fly · Lake");
        Sut.CatchesTotal.Should().Be("20");
        Sut.TripsTotal.Should().Be("6");
        Sut.SpeciesTotal.Should().Be("4");
        Sut.FilteredCatchesTotal.Should().Be("3");
        Sut.FilteredTripsTotal.Should().Be("1");
        Sut.FilteredSpeciesTotal.Should().Be("2");
        await MockModals.Received(1).EditFiltersAsync(Arg.Is<DashboardFilterSelectionModel>(selection => selection.Preset == DashboardDatePresetEnum.ThisYear), Arg.Is<DashboardFilterOptionsModel>(options => options.Species.Count == 0 && options.Methods.Count == 0 && options.Venues.Count == 0), Arg.Any<CancellationToken>());
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == "Pike" && filter.Method == "Fly" && filter.Venue == "Lake" && filter.FromInclusive == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryTheSubmittedFiltersWithoutClaimingAFailedRequestWasApplied()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var summary = Sut.AppliedFilterSummary;
        ChooseFilters(new DashboardFilterSelectionModel { Preset = DashboardDatePresetEnum.AllTime, Species = "Pike" });
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<DashboardDto>.Failed(ApiOutcomeEnum.Unavailable), ApiResultModel<DashboardDto>.Succeeded(new DashboardDto()));
        await Sut.EditFiltersAsync(CancellationToken.None);
        Sut.AppliedFilterSummary.Should().Be(summary);
        Sut.HasStatus.Should().BeTrue();

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.HasStatus.Should().BeFalse();
        Sut.HasActiveFilters.Should().BeTrue();
        Sut.AppliedFilterSummary.Should().Contain("Pike");
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == "Pike" && filter.FromInclusive == null), Arg.Any<CancellationToken>());
        await MockModals.Received(1).EditFiltersAsync(Arg.Is<DashboardFilterSelectionModel>(selection => selection.Preset == DashboardDatePresetEnum.ThisYear), Arg.Is<DashboardFilterOptionsModel>(options => options.Species.Count == 0 && options.Methods.Count == 0 && options.Venues.Count == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAModalResultAfterTheAccountChanges()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var pending = new TaskCompletionSource<DashboardFilterSelectionModel?>();
        MockModals.EditFiltersAsync(Arg.Any<DashboardFilterSelectionModel>(), Arg.Any<DashboardFilterOptionsModel>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var editing = Sut.EditFiltersAsync(CancellationToken.None);

        // Act
        MockSession.IsCurrent(Lease).Returns(false);
        AccountCancellation.Cancel();
        pending.SetResult(new DashboardFilterSelectionModel { Preset = DashboardDatePresetEnum.AllTime, Species = "Pike" });
        await editing;

        // Assert
        Sut.Data.Should().BeNull();
        Sut.IsFilterModalOpen.Should().BeFalse();
        Sut.HasActiveFilters.Should().BeFalse();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
        await MockModals.Received(1).EditFiltersAsync(Arg.Is<DashboardFilterSelectionModel>(selection => selection.Preset == DashboardDatePresetEnum.ThisYear), Arg.Is<DashboardFilterOptionsModel>(options => options.Species.Count == 0 && options.Methods.Count == 0 && options.Venues.Count == 0), Arg.Is<CancellationToken>(token => token.IsCancellationRequested));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldIgnoreLateApiOrUnitResponsesAfterThePageLifetimeEnds(bool awaitingUnits)
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var original = Sut.Data;
        ChooseFilters(new DashboardFilterSelectionModel { Preset = DashboardDatePresetEnum.AllTime });
        var pending = new TaskCompletionSource<ApiResultModel<DashboardDto>>();
        var units = new TaskCompletionSource<MeasurementUnitsModel>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(units.Task);
        if (awaitingUnits)
        {
            pending.SetResult(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 99 } }));
        }
        using var cancellation = new CancellationTokenSource();
        var editing = Sut.EditFiltersAsync(cancellation.Token);

        // Act
        cancellation.Cancel();
        pending.TrySetResult(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 99 } }));
        units.SetResult(MeasurementUnitsModel.Default);
        await editing;

        // Assert
        Sut.Data.Should().BeSameAs(original);
        Sut.IsLoading.Should().BeFalse();
        Sut.HasActiveFilters.Should().BeFalse();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive == null), Arg.Any<CancellationToken>());
        await MockUnits.Received(awaitingUnits ? 2 : 1).GetAsync(Arg.Any<CancellationToken>());
        await MockModals.Received(1).EditFiltersAsync(Arg.Is<DashboardFilterSelectionModel>(selection => selection.Preset == DashboardDatePresetEnum.ThisYear),
            Arg.Is<DashboardFilterOptionsModel>(options => options.Species.Count == 0 && options.Methods.Count == 0 && options.Venues.Count == 0), Arg.Any<CancellationToken>());
    }
}
