using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Tests.Measurements.MeasurementServiceTests;

public class WhenTestingFormatting : BaseMeasurementServiceTest
{
    [Fact]
    public void ItShouldFormatAMissingWeightAsEmpty()
    {
        // Arrange
        // Act
        var result = Sut.FormatWeight(null, WeightUnitEnum.Kg, "kg", "oz");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldFormatAMetricWeightWithItsUnitLabel()
    {
        // Arrange
        // Act
        var result = Sut.FormatWeight(2m, WeightUnitEnum.Kg, "kg", "oz");

        // Assert
        result.Should().Be("2 kg");
    }

    [Fact]
    public void ItShouldFormatAnImperialWeightAsPoundsAndOunces()
    {
        // Arrange
        // Act
        var result = Sut.FormatWeight(1m, WeightUnitEnum.Lb, "lb", "oz");

        // Assert
        result.Should().Be("2 lb 3 oz");
    }

    [Fact]
    public void ItShouldFormatAMissingLengthAsEmpty()
    {
        // Arrange
        // Act
        var result = Sut.FormatLength(null, LengthUnitEnum.Cm, "cm");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldFormatAMetricLengthWithItsUnitLabel()
    {
        // Arrange
        // Act
        var result = Sut.FormatLength(40m, LengthUnitEnum.Cm, "cm");

        // Assert
        result.Should().Be("40 cm");
    }

    [Fact]
    public void ItShouldConvertAnImperialLengthToInchesBeforeFormatting()
    {
        // Arrange
        // Act
        var result = Sut.FormatLength(25.4m, LengthUnitEnum.In, "in");

        // Assert
        result.Should().Be("10 in");
    }

    [Fact]
    public void ItShouldTreatAMissingWeightAsZeroPoundsAndOunces()
    {
        // Arrange
        // Act
        var result = Sut.ToPoundsAndOunces(null);

        // Assert
        result.Should().Be((0, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 0)]
    public void ItShouldNotConvertNegativeOrZeroPoundsAndOunces(int pounds, int ounces)
    {
        // Arrange
        // Act
        var result = Sut.FromPoundsAndOunces(pounds, ounces);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ItShouldConvertPoundsAndOuncesToKilogramsRoundedToGrams()
    {
        // Arrange
        // Act
        var result = Sut.FromPoundsAndOunces(2, 3);

        // Assert
        result.Should().Be(0.992m);
    }
}
