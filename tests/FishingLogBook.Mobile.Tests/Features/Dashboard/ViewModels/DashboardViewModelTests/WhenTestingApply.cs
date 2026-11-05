using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardViewModelTests;

public class WhenTestingApply : BaseDashboardViewModelTest
{
    [Fact]
    public async Task ItShouldKeepInvalidCustomDraftsExpandedAndPreserveTheAppliedSummary()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var summary = Sut.AppliedFilterSummary;
        Sut.ToggleFilters();
        Sut.SelectPreset((int)DashboardDatePresetEnum.Custom);
        Sut.FromDate = new DateTime(2026, 3, 2);
        Sut.ToDate = new DateTime(2026, 3, 1);

        // Act
        await Sut.ApplyAsync(CancellationToken.None);

        // Assert
        Sut.StatusText.Should().Contain("date");
        Sut.IsFiltersExpanded.Should().BeTrue();
        Sut.AppliedFilterSummary.Should().Be(summary);
        Sut.HasActiveFilters.Should().BeFalse();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSummarizeTheSubmittedSnapshotAndDisplayZeroCardsWhenNothingMatches()
    {
        // Arrange
        var pending = new TaskCompletionSource<ApiResultModel<DashboardDto>>();
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        Sut.ToggleFilters();
        Sut.SelectPreset((int)DashboardDatePresetEnum.Custom);
        Sut.FromDate = Sut.ToDate = new DateTime(2026, 3, 1);

        // Act
        var apply = Sut.ApplyAsync(CancellationToken.None);
        Sut.FromDate = new DateTime(2026, 4, 1);
        pending.SetResult(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 10 } }));
        await apply;

        // Assert
        Sut.AppliedFilterSummary.Should().StartWith("01/03/2026 – 01/03/2026");
        Sut.FilteredCatchesTotal.Should().Be("0");
        Sut.FilteredTripsTotal.Should().Be("0");
        Sut.FilteredSpeciesTotal.Should().Be("0");
        Sut.ShowStatisticsEmpty.Should().BeTrue();
        Sut.ShowFilteredEmpty.Should().BeTrue();
        Sut.StatisticsEmptyText.Should().Contain("match");
        Sut.ShowImportSuggestion.Should().BeFalse();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive == DateTimeOffset.Parse("2026-03-01T05:00Z")
            && filter.ToExclusive == DateTimeOffset.Parse("2026-03-02T05:00Z")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepFailedOrInvalidFiltersExpandedWithoutClaimingTheyAreApplied()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var summary = Sut.AppliedFilterSummary;
        Sut.ToggleFilters();
        Sut.SelectPreset((int)DashboardDatePresetEnum.AllTime);
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<DashboardDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        await Sut.ApplyAsync(CancellationToken.None);

        // Assert
        Sut.IsFiltersExpanded.Should().BeTrue();
        Sut.AppliedFilterSummary.Should().Be(summary);
        Sut.HasActiveFilters.Should().BeFalse();
        Sut.FilteredCatchesTotal.Should().BeEmpty();
        Sut.HasStatus.Should().BeTrue();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive == null), Arg.Any<CancellationToken>());
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive.HasValue), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshAppliedFiltersWithoutApplyingPendingEdits()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        Sut.SelectPreset((int)DashboardDatePresetEnum.AllTime);
        Sut.SelectedSpecies = new DashboardChoiceModel("Pike", "Pike");

        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.HasActiveFilters.Should().BeFalse();
        Sut.AppliedFilterSummary.Should().Be("This year · All species · All methods");
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive.HasValue && filter.Species == null), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("en-GB", "This year · All species · All methods")]
    [InlineData("fr-FR", "Cette année · Toutes les espèces · Toutes les techniques")]
    public async Task ItShouldOnlyUpdateTheAppliedSummaryAfterSuccessAndCollapseThenReset(string culture, string defaultSummary)
    {
        // Arrange
        Text.SetCulture(culture);
        MockClient.GetAsync(Arg.Any<DashboardFilterDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<DashboardDto>.Succeeded(new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 20, Trips = 6, Species = 4 },
            Filtered = new DashboardTotalsDto { Catches = 3, Trips = 1, Species = 2 },
            Species = ["Pike"],
            Methods = ["Fly"],
            Venues = ["Lake"]
        }));
        await Sut.LoadAsync(CancellationToken.None);
        Sut.IsFiltersExpanded.Should().BeFalse();
        Sut.ToggleFilters();
        Sut.SelectPreset((int)DashboardDatePresetEnum.AllTime);
        Sut.SelectedSpecies = new DashboardChoiceModel("Pike", "Pike");
        Sut.SelectedMethod = new DashboardChoiceModel("Fly", "Fly");
        Sut.SelectedVenue = new DashboardChoiceModel("Lake", "Lake");
        Sut.AppliedFilterSummary.Should().Be(defaultSummary);
        Sut.HasActiveFilters.Should().BeFalse();

        // Act
        await Sut.ApplyAsync(CancellationToken.None);

        // Assert
        Sut.IsFiltersExpanded.Should().BeFalse();
        Sut.HasActiveFilters.Should().BeTrue();
        Sut.AppliedFilterSummary.Should().Contain("Pike · Fly · Lake");
        Sut.CatchesTotal.Should().Be("20");
        Sut.TripsTotal.Should().Be("6");
        Sut.SpeciesTotal.Should().Be("4");
        Sut.FilteredCatchesTotal.Should().Be("3");
        Sut.FilteredTripsTotal.Should().Be("1");
        Sut.FilteredSpeciesTotal.Should().Be("2");
        Sut.ToggleFilters();
        Sut.IsFiltersExpanded.Should().BeTrue();
        await Sut.ResetAsync(CancellationToken.None);
        Sut.IsFiltersExpanded.Should().BeFalse();
        Sut.AppliedFilterSummary.Should().Be(defaultSummary);
        Sut.HasActiveFilters.Should().BeFalse();
        await MockClient.Received(1).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive == null
            && filter.Species == "Pike" && filter.Method == "Fly" && filter.Venue == "Lake"), Arg.Any<CancellationToken>());
        await MockClient.Received(2).GetAsync(Arg.Is<DashboardFilterDto>(filter => filter.FromInclusive.HasValue
            && filter.Species == null && filter.Method == null && filter.Venue == null), Arg.Any<CancellationToken>());
    }
}
