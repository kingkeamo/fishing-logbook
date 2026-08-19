using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.MeasurementServiceTests;

public class WhenTestingToDisplayWeight : BaseMeasurementServiceTest
{
    [Fact]
    public void ItShouldReturnNothingWhenThereIsNoStoredWeight()
    {
        // Arrange
        // Act
        var kilograms = Sut.ToDisplayWeight(null, WeightUnitEnum.Kg);
        var pounds = Sut.ToDisplayWeight(null, WeightUnitEnum.Lb);

        // Assert
        kilograms.Should().BeNull();
        pounds.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnTheCanonicalValueUnchangedForKilograms()
    {
        // Arrange
        // Act
        var display = Sut.ToDisplayWeight(2.041m, WeightUnitEnum.Kg);

        // Assert
        display.Should().Be(2.041m);
    }

    [Theory]
    [InlineData(0.454, 1.00)]
    [InlineData(2.041, 4.50)]
    [InlineData(4.649, 10.25)]
    [InlineData(453.139, 999.00)]
    public void ItShouldConvertKilogramsToPoundsAtTwoDecimalPlaces(decimal kilograms, decimal expectedPounds)
    {
        // Arrange
        // Act
        var display = Sut.ToDisplayWeight(kilograms, WeightUnitEnum.Lb);

        // Assert
        display.Should().Be(expectedPounds);
    }
}
