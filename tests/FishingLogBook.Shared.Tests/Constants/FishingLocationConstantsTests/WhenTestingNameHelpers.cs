using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.FishingLocationConstantsTests;

public class WhenTestingNameHelpers
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Lough Corrib", true)]
    public void ItShouldRequireANameWithContent(string? name, bool expected)
    {
        // Arrange
        // Act
        var result = FishingLocationConstants.IsNameValid(name);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ItShouldMeasureNameLengthAfterTrimmingAtTheMaximumBoundary()
    {
        // Arrange
        var atLimit = new string('a', FishingLocationConstants.MaxNameLength);
        var overLimit = new string('a', FishingLocationConstants.MaxNameLength + 1);

        // Act
        var padded = FishingLocationConstants.IsNameValid($" {atLimit} ");
        var tooLong = FishingLocationConstants.IsNameValid(overLimit);

        // Assert
        padded.Should().BeTrue();
        tooLong.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("  Lough Corrib ", "Lough Corrib")]
    public void ItShouldTrimNames(string? name, string? expected)
    {
        // Arrange
        // Act
        var result = FishingLocationConstants.TrimName(name);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Lough Corrib", " lough corrib ", true)]
    [InlineData("Lough Corrib", "Lough Mask", false)]
    [InlineData(null, "  ", true)]
    [InlineData(null, "Lough Corrib", false)]
    public void ItShouldComparePlaceNamesIgnoringCaseAndSurroundingWhitespace(string? left, string? right, bool expected)
    {
        // Arrange
        // Act
        var result = FishingLocationConstants.AreSameName(left, right);

        // Assert
        result.Should().Be(expected);
    }
}
