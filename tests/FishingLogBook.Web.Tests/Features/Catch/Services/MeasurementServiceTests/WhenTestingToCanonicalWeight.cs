using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.MeasurementServiceTests;

public class WhenTestingToCanonicalWeight : BaseMeasurementServiceTest
{
    [Fact]
    public void ItShouldReturnNothingWhenTheFieldIsCleared()
    {
        // Arrange
        // Act
        var canonical = Sut.ToCanonicalWeight(null, WeightUnitEnum.Lb, 2.041m);

        // Assert
        canonical.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnTheEnteredValueUnchangedForKilograms()
    {
        // Arrange
        // Act
        var canonical = Sut.ToCanonicalWeight(2.041m, WeightUnitEnum.Kg, null);

        // Assert
        canonical.Should().Be(2.041m);
    }

    [Theory]
    [InlineData(1.00, 0.454)]
    [InlineData(4.50, 2.041)]
    [InlineData(10.25, 4.649)]
    public void ItShouldConvertPoundsToKilogramsAtThreeDecimalPlaces(decimal pounds, decimal expectedKilograms)
    {
        // Arrange
        // Act
        var canonical = Sut.ToCanonicalWeight(pounds, WeightUnitEnum.Lb, null);

        // Assert
        canonical.Should().Be(expectedKilograms);
    }

    [Fact]
    public void ItShouldKeepTheStoredKilogramsWhenTheDisplayedPoundsAreUnchanged()
    {
        // Arrange
        const decimal storedKilograms = 2.0413m;
        var displayed = Sut.ToDisplayWeight(storedKilograms, WeightUnitEnum.Lb);

        // Act
        var canonical = Sut.ToCanonicalWeight(displayed, WeightUnitEnum.Lb, storedKilograms);

        // Assert
        canonical.Should().Be(storedKilograms);
    }

    [Fact]
    public void ItShouldNotDriftAcrossRepeatedOpenAndSaveCyclesInPounds()
    {
        // Arrange
        var canonical = Sut.ToCanonicalWeight(4.50m, WeightUnitEnum.Lb, null);
        var firstDisplay = Sut.ToDisplayWeight(canonical, WeightUnitEnum.Lb);

        // Act
        for (var cycle = 0; cycle < 5; cycle++)
        {
            var display = Sut.ToDisplayWeight(canonical, WeightUnitEnum.Lb);
            canonical = Sut.ToCanonicalWeight(display, WeightUnitEnum.Lb, canonical);
        }

        // Assert
        firstDisplay.Should().Be(4.50m);
        Sut.ToDisplayWeight(canonical, WeightUnitEnum.Lb).Should().Be(4.50m);
        canonical.Should().Be(2.041m);
    }

    [Fact]
    public void ItShouldConvertAChangedPoundValueToNewKilograms()
    {
        // Arrange
        const decimal storedKilograms = 2.041m;

        // Act
        var canonical = Sut.ToCanonicalWeight(5.00m, WeightUnitEnum.Lb, storedKilograms);

        // Assert
        canonical.Should().Be(2.268m);
    }
}
