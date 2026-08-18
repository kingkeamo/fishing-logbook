using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.MeasurementServiceTests;

public class WhenTestingMaxDisplayWeight : BaseMeasurementServiceTest
{
    [Fact]
    public void ItShouldReturnTheCanonicalLimitForKilograms()
    {
        // Arrange
        // Act
        var limit = Sut.MaxDisplayWeight(WeightUnitEnum.Kg);

        // Assert
        limit.Should().Be(CatchDetailConstants.MaxWeightKilograms);
    }

    [Fact]
    public void ItShouldReturnALimitInPoundsThatStillConvertsWithinTheCanonicalLimit()
    {
        // Arrange
        // Act
        var limit = Sut.MaxDisplayWeight(WeightUnitEnum.Lb);

        // Assert
        limit.Should().Be(2204.62m);
        var canonical = Sut.ToCanonicalWeight(limit, WeightUnitEnum.Lb, null);
        CatchDetailConstants.IsWeightValid(canonical).Should().BeTrue();
    }
}
