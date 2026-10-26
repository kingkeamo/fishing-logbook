using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingClearFilters : BaseCatchListViewModelTest
{
    [Fact]
    public void ItShouldRestoreAllCatchesGroupedByDateWithNewestFirst()
    {
        // Arrange
        Sut.SetSearch("no matches");
        Sut.SetMethod("Fly");
        // Act
        Sut.ClearFilters();
        // Assert
        Sut.SearchTerm.Should().BeEmpty();
        Sut.HasFilters.Should().BeFalse();
        Sut.HasNoMatches.Should().BeFalse();
        Sut.Catches.Should().HaveCount(4).And.BeInDescendingOrder(item => item.RecordedAt);
        Sut.Groups.Select(group => group.Day).Should().Equal(new DateOnly(2026, 6, 14), new DateOnly(2026, 6, 13));
        Sut.Groups.Should().OnlyContain(group => group.Count == 2 && group.All(item => item.Day == group.Day));
        Sut.Groups.SelectMany(group => group).Should().Equal(Sut.Catches);
    }
}
