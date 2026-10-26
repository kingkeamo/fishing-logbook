using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchFilterModelTests;

public class WhenTestingActiveCount
{
    [Fact]
    public void ItShouldBeZeroForTheDefaultFilter()
    {
        // Arrange
        // Act
        var count = CatchFilterModel.None.ActiveCount;
        // Assert
        count.Should().Be(0);
    }

    [Theory]
    [InlineData(CatchMethodEnum.Fly, CatchDateRangeEnum.AllTime, "", 0, 1)]
    [InlineData(CatchMethodEnum.All, CatchDateRangeEnum.Last7Days, "", 0, 1)]
    [InlineData(CatchMethodEnum.All, CatchDateRangeEnum.AllTime, "Corrib", 0, 1)]
    [InlineData(CatchMethodEnum.All, CatchDateRangeEnum.AllTime, "   ", 0, 0)]
    [InlineData(CatchMethodEnum.All, CatchDateRangeEnum.AllTime, "", 3, 3)]
    [InlineData(CatchMethodEnum.Lure, CatchDateRangeEnum.Last30Days, "Mask", 2, 5)]
    public void ItShouldCountEachActiveCriterionAndEverySelectedSpecies(CatchMethodEnum method, CatchDateRangeEnum range, string location, int species, int expected)
    {
        // Arrange
        var selected = Enum.GetValues<CatchSpeciesEnum>().Take(species).ToHashSet();
        // Act
        var filter = new CatchFilterModel(method, selected, range, location);
        // Assert
        filter.ActiveCount.Should().Be(expected);
    }
}
