using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.AnglerLookupConstantsTests;

public class WhenTestingQueryHelpers
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  sam  ", "sam")]
    public void ItShouldTrimTheQueryAndTreatBlankAsMissing(string? query, string? expected)
    {
        // Arrange
        // Act
        var result = AnglerLookupConstants.TrimQuery(query);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ab  ")]
    public void ItShouldRejectAQueryShorterThanTheMinimumAfterTrimming(string? query)
    {
        // Arrange
        // Act
        var result = AnglerLookupConstants.IsQueryValid(query);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRejectAQueryLongerThanTheMaximum()
    {
        // Arrange
        var query = new string('a', AnglerLookupConstants.MaxQueryLength + 1);

        // Act
        var result = AnglerLookupConstants.IsQueryValid(query);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(AnglerLookupConstants.MinQueryLength)]
    [InlineData(AnglerLookupConstants.MaxQueryLength)]
    public void ItShouldAcceptAQueryAtTheLengthBoundaries(int length)
    {
        // Arrange
        var query = $" {new string('a', length)} ";

        // Act
        var result = AnglerLookupConstants.IsQueryValid(query);

        // Assert
        result.Should().BeTrue();
    }
}
