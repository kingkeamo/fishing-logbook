using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.MeasurementServiceTests;

public class WhenTestingToCanonicalLength : BaseMeasurementServiceTest
{
    [Fact]
    public void ItShouldReturnNothingWhenTheFieldIsCleared()
    {
        // Arrange
        // Act
        var canonical = Sut.ToCanonicalLength(null, LengthUnitEnum.In, 46.36m);

        // Assert
        canonical.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnTheEnteredValueUnchangedForCentimetres()
    {
        // Arrange
        // Act
        var canonical = Sut.ToCanonicalLength(46.36m, LengthUnitEnum.Cm, null);

        // Assert
        canonical.Should().Be(46.36m);
    }

    [Theory]
    [InlineData(1.00, 2.54)]
    [InlineData(18.25, 46.36)]
    [InlineData(39.37, 100.00)]
    public void ItShouldConvertInchesToCentimetresAtTwoDecimalPlaces(decimal inches, decimal expectedCentimetres)
    {
        // Arrange
        // Act
        var canonical = Sut.ToCanonicalLength(inches, LengthUnitEnum.In, null);

        // Assert
        canonical.Should().Be(expectedCentimetres);
    }

    [Fact]
    public void ItShouldKeepTheStoredCentimetresWhenTheDisplayedInchesAreUnchanged()
    {
        // Arrange
        const decimal storedCentimetres = 46.357m;
        var displayed = Sut.ToDisplayLength(storedCentimetres, LengthUnitEnum.In);

        // Act
        var canonical = Sut.ToCanonicalLength(displayed, LengthUnitEnum.In, storedCentimetres);

        // Assert
        canonical.Should().Be(storedCentimetres);
    }

    [Fact]
    public void ItShouldNotDriftAcrossRepeatedOpenAndSaveCyclesInInches()
    {
        // Arrange
        var canonical = Sut.ToCanonicalLength(18.25m, LengthUnitEnum.In, null);

        // Act
        for (var cycle = 0; cycle < 5; cycle++)
        {
            var display = Sut.ToDisplayLength(canonical, LengthUnitEnum.In);
            canonical = Sut.ToCanonicalLength(display, LengthUnitEnum.In, canonical);
        }

        // Assert
        canonical.Should().Be(46.36m);
        Sut.ToDisplayLength(canonical, LengthUnitEnum.In).Should().Be(18.25m);
    }
}
