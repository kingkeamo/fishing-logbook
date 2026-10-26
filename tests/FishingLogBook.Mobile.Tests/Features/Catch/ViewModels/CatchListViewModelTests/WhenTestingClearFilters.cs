using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingClearFilters : BaseCatchListViewModelTest
{
    [Fact]
    public void ItShouldRestoreAllCatchesGroupedByDateWithNewestFirst()
    {
        // Arrange
        var original = Sut.Catches[0];
        Sut.SetSearch("no matches");
        Sut.SetMethod(CatchMethodEnum.Fly);
        // Act
        Sut.ClearFilters();
        // Assert
        Sut.SearchTerm.Should().BeEmpty();
        Sut.HasFilters.Should().BeFalse();
        Sut.HasNoMatches.Should().BeFalse();
        Sut.Catches.Should().HaveCount(7).And.BeInDescendingOrder(item => item.RecordedAt);
        Sut.Groups.Select(group => group.Day).Should().Equal(new DateOnly(2026, 6, 14), new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 5), new DateOnly(2026, 5, 8));
        Sut.Groups.Select(group => group.Count).Should().Equal(2, 1, 1, 2, 1);
        Sut.Groups.Should().OnlyContain(group => group.All(item => item.Day == group.Day));
        Sut.Groups.SelectMany(group => group).Should().Equal(Sut.Catches);
        Sut.Catches.Should().Contain(item => ReferenceEquals(item, original));
    }

    [Fact]
    public void ItShouldResetSpeciesDateRangeAndLocationFilters()
    {
        // Arrange
        Sut.ApplyFilters(new CatchFilterModel(CatchMethodEnum.Fly, new HashSet<CatchSpeciesEnum> { CatchSpeciesEnum.Pike }, CatchDateRangeEnum.Last7Days, "Mask"));
        // Act
        Sut.ClearFilters();
        // Assert
        Sut.Filters.Should().Be(CatchFilterModel.None);
        Sut.ActiveFilterCount.Should().Be(0);
        Sut.HasAdvancedFilters.Should().BeFalse();
        Sut.Catches.Should().HaveCount(7);
    }
}
