using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.MeasurementServiceTests;

public class WhenTestingImperialWeight : BaseMeasurementServiceTest
{
    [Fact]
    public void ItShouldRollSixteenOuncesIntoTheNextPound()
    {
        // Arrange
        var canonical = Sut.FromPoundsAndOunces(3, 16);

        // Act
        var displayed = Sut.ToPoundsAndOunces(canonical);

        // Assert
        displayed.Should().Be((4, 0));
    }

    [Fact]
    public void ItShouldFormatImperialWeightForAnglers()
    {
        // Arrange
        var canonical = Sut.FromPoundsAndOunces(3, 12);

        // Act
        var displayed = Sut.FormatWeight(canonical, WeightUnitEnum.Lb, "lb", "oz");

        // Assert
        displayed.Should().Be("3 lb 12 oz");
    }

    [Fact]
    public void ItShouldTreatZeroAsNoMeasurement()
    {
        // Arrange
        // Act
        var canonical = Sut.FromPoundsAndOunces(0, 0);

        // Assert
        canonical.Should().BeNull();
    }
}
