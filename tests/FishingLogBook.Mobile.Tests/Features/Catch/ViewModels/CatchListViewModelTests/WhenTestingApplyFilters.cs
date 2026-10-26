using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingApplyFilters : BaseCatchListViewModelTest
{
    private static CatchFilterModel Filter(
        CatchMethodEnum method = CatchMethodEnum.All,
        CatchDateRangeEnum range = CatchDateRangeEnum.AllTime,
        string location = "",
        params CatchSpeciesEnum[] species)
    {
        return new CatchFilterModel(method, species.ToHashSet(), range, location);
    }

    [Fact]
    public void ItShouldMatchAnySelectedSpecies()
    {
        // Arrange
        // Act
        Sut.ApplyFilters(Filter(species: [CatchSpeciesEnum.Pike, CatchSpeciesEnum.AtlanticSalmon]));
        // Assert
        Sut.Catches.Should().HaveCount(2).And.OnlyContain(item => item.SpeciesIdentifier == CatchSpeciesEnum.Pike || item.SpeciesIdentifier == CatchSpeciesEnum.AtlanticSalmon);
        Sut.ActiveFilterCount.Should().Be(2);
        Sut.HasAdvancedFilters.Should().BeTrue();
        Sut.HasFilters.Should().BeTrue();
    }

    [Fact]
    public void ItShouldCombineMethodSpeciesAndLocationAsAnd()
    {
        // Arrange
        // Act
        Sut.ApplyFilters(Filter(CatchMethodEnum.Fly, location: "cong", species: [CatchSpeciesEnum.BrownTrout]));
        // Assert
        Sut.Catches.Should().ContainSingle().Which.Place.Should().Be("Cong River");
        Sut.ActiveFilterCount.Should().Be(3);
    }

    [Fact]
    public void ItShouldReturnNoMatchesWhenCombinedFiltersExcludeEveryCatch()
    {
        // Arrange
        // Act
        Sut.ApplyFilters(Filter(CatchMethodEnum.Lure, species: [CatchSpeciesEnum.BrownTrout]));
        // Assert
        Sut.Catches.Should().BeEmpty();
        Sut.HasNoMatches.Should().BeTrue();
    }

    [Theory]
    [InlineData(CatchDateRangeEnum.AllTime, 7)]
    [InlineData(CatchDateRangeEnum.Last30Days, 6)]
    [InlineData(CatchDateRangeEnum.Last7Days, 4)]
    public void ItShouldFilterByDateRangeRelativeToTheClock(CatchDateRangeEnum range, int count)
    {
        // Arrange
        // Act
        Sut.ApplyFilters(Filter(range: range));
        // Assert
        Sut.Catches.Should().HaveCount(count);
        Sut.ActiveFilterCount.Should().Be(range == CatchDateRangeEnum.AllTime ? 0 : 1);
    }

    [Theory]
    [InlineData(2026, 6, 21, 9, 29, 1)]
    [InlineData(2026, 6, 21, 9, 30, 0)]
    public void ItShouldExcludeACatchRecordedExactlySevenDaysAgo(int year, int month, int day, int hour, int minute, int count)
    {
        // Arrange
        var clock = new FixedTimeProvider(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero));
        var sut = new CatchListViewModel(Text, new SampleCatchProvider(Text, Clock), clock);
        // Act
        sut.ApplyFilters(Filter(range: CatchDateRangeEnum.Last7Days));
        // Assert
        sut.Catches.Should().HaveCount(count);
    }

    [Fact]
    public void ItShouldMatchLocationAcrossLanguageChanges()
    {
        // Arrange
        Sut.ApplyFilters(Filter(location: "mask"));
        // Act
        Text.SetCulture("fr-FR");
        // Assert
        Sut.Catches.Should().HaveCount(2).And.OnlyContain(item => item.Place.Contains("Mask"));
        Sut.Filters.Location.Should().Be("mask");
    }

    [Fact]
    public void ItShouldCombineAppliedFiltersWithSearch()
    {
        // Arrange
        Sut.ApplyFilters(Filter(CatchMethodEnum.Fly));
        // Act
        Sut.SetSearch("rainbow");
        // Assert
        Sut.Catches.Should().HaveCount(2).And.OnlyContain(item => item.SpeciesIdentifier == CatchSpeciesEnum.RainbowTrout);
    }

    [Fact]
    public void ItShouldCreateADraftWithSpeciesCountsAndCurrentSelection()
    {
        // Arrange
        Sut.ApplyFilters(Filter(species: [CatchSpeciesEnum.Pike]));
        // Act
        var draft = Sut.CreateFilterDraft();
        // Assert
        draft.Species.Select(option => (option.Species, option.Count, option.IsSelected)).Should().Equal(
            (CatchSpeciesEnum.AtlanticSalmon, 1, false),
            (CatchSpeciesEnum.BrownTrout, 2, false),
            (CatchSpeciesEnum.RainbowTrout, 2, false),
            (CatchSpeciesEnum.Pike, 1, true),
            (CatchSpeciesEnum.SeaTrout, 0, false),
            (CatchSpeciesEnum.Other, 1, false));
        draft.ToFilter().Should().BeEquivalentTo(Sut.Filters);
    }

    [Fact]
    public void ItShouldNotChangeResultsUntilADraftIsApplied()
    {
        // Arrange
        var draft = Sut.CreateFilterDraft();
        // Act
        draft.SetMethod(CatchMethodEnum.Lure);
        // Assert
        Sut.Catches.Should().HaveCount(7);
        Sut.Filters.Should().Be(CatchFilterModel.None);
    }
}
