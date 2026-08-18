using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.MeasurementServiceTests;

public class WhenTestingToDisplayLength : BaseMeasurementServiceTest
{
    [Fact]
    public void ItShouldReturnNothingWhenThereIsNoStoredLength()
    {
        // Arrange
        // Act
        var display = Sut.ToDisplayLength(null, LengthUnitEnum.In);

        // Assert
        display.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnTheCanonicalValueUnchangedForCentimetres()
    {
        // Arrange
        // Act
        var display = Sut.ToDisplayLength(46.36m, LengthUnitEnum.Cm);

        // Assert
        display.Should().Be(46.36m);
    }

    [Theory]
    [InlineData(2.54, 1.00)]
    [InlineData(46.36, 18.25)]
    [InlineData(100.00, 39.37)]
    public void ItShouldConvertCentimetresToInchesAtTwoDecimalPlaces(decimal centimetres, decimal expectedInches)
    {
        // Arrange
        // Act
        var display = Sut.ToDisplayLength(centimetres, LengthUnitEnum.In);

        // Assert
        display.Should().Be(expectedInches);
    }
}
