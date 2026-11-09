using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.TripConstantsTests;

public class WhenTestingTextHelpers
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ItShouldTreatABlankPlaceNameAsMissing(string? placeName)
    {
        // Arrange
        // Act
        var result = TripConstants.TrimPlaceName(placeName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ItShouldTrimAPlaceNameAtTheMaximumLengthAndRejectOneCharacterMore()
    {
        // Arrange
        var atLimit = new string('a', TripConstants.MaxPlaceNameLength);
        var overLimit = new string('a', TripConstants.MaxPlaceNameLength + 1);

        // Act
        var accepted = TripConstants.TrimPlaceName($"  {atLimit}  ");
        var rejected = TripConstants.TrimPlaceName(overLimit);

        // Assert
        accepted.Should().Be(atLimit);
        rejected.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ItShouldTreatABlankTitleAsMissing(string? title)
    {
        // Arrange
        // Act
        var result = TripConstants.TrimTitle(title);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ItShouldTrimATitleAtTheMaximumLengthAndRejectOneCharacterMore()
    {
        // Arrange
        var atLimit = new string('a', TripConstants.MaxTitleLength);
        var overLimit = new string('a', TripConstants.MaxTitleLength + 1);

        // Act
        var accepted = TripConstants.TrimTitle($" {atLimit} ");
        var rejected = TripConstants.TrimTitle(overLimit);

        // Assert
        accepted.Should().Be(atLimit);
        rejected.Should().BeNull();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Fish rising", true)]
    public void ItShouldRequireNoteTextWithContent(string? text, bool expected)
    {
        // Arrange
        // Act
        var result = TripConstants.IsNoteTextValid(text);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ItShouldMeasureNoteTextLengthAfterTrimmingAtTheMaximumBoundary()
    {
        // Arrange
        var atLimit = new string('a', TripConstants.MaxNoteTextLength);
        var overLimit = new string('a', TripConstants.MaxNoteTextLength + 1);

        // Act
        var padded = TripConstants.IsNoteTextValid($"  {atLimit}  ");
        var tooLong = TripConstants.IsNoteTextValid(overLimit);

        // Assert
        padded.Should().BeTrue();
        tooLong.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("  Fish rising  ", "Fish rising")]
    public void ItShouldTrimNoteText(string? text, string? expected)
    {
        // Arrange
        // Act
        var result = TripConstants.TrimNoteText(text);

        // Assert
        result.Should().Be(expected);
    }
}
