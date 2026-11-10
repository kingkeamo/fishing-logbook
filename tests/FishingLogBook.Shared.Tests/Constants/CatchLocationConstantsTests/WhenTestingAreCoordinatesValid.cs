using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.CatchLocationConstantsTests;

public class WhenTestingAreCoordinatesValid
{
    [Theory]
    [InlineData(-90.0001, 0)]
    [InlineData(90.0001, 0)]
    [InlineData(0, -180.0001)]
    [InlineData(0, 180.0001)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    public void ItShouldRejectCoordinatesOutsideTheValidRange(double latitude, double longitude)
    {
        // Arrange
        // Act
        var result = CatchLocationConstants.AreCoordinatesValid(latitude, longitude);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    [InlineData(53.3498, -6.2603)]
    public void ItShouldAcceptCoordinatesWithinTheValidRangeIncludingTheBoundaries(double latitude, double longitude)
    {
        // Arrange
        // Act
        var result = CatchLocationConstants.AreCoordinatesValid(latitude, longitude);

        // Assert
        result.Should().BeTrue();
    }
}
