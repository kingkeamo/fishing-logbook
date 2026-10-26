using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingSetSearch : BaseCatchListViewModelTest
{
    [Theory]
    [InlineData(null, 7)]
    [InlineData("   ", 7)]
    [InlineData(" TROUT ", 4)]
    [InlineData("salmon", 1)]
    [InlineData("Spinning", 2)]
    [InlineData("not a species", 0)]
    public void ItShouldSearchDisplayedCatchFactsIgnoringCaseAndOuterWhitespace(string? query, int count)
    {
        // Arrange
        // Act
        Sut.SetSearch(query);
        // Assert
        Sut.Catches.Should().HaveCount(count);
        Sut.HasNoMatches.Should().Be(count == 0);
        Sut.Groups.SelectMany(group => group).Should().Equal(Sut.Catches);
    }

    [Fact]
    public void ItShouldCombineSearchWithMethodAndRetainBothAcrossLanguageChanges()
    {
        // Arrange
        Sut.SetMethod(CatchMethodEnum.Spinning);
        Sut.SetSearch("truite");
        // Act
        Text.SetCulture("fr-FR");
        // Assert
        Sut.Catches.Should().ContainSingle().Which.Species.Should().Be("Truite fario");
        Sut.Catches[0].Method.Should().Be(Text["MethodSpinning"]);
        Sut.Catches[0].Measurements.Should().Be("0,8 kg · 29 cm");
        Sut.SearchTerm.Should().Be("truite");
        Sut.SelectedMethod.Should().Be(CatchMethodEnum.Spinning);
    }
}
