using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.MeasurementServiceTests;

public class WhenTestingMaxDisplayLength : BaseMeasurementServiceTest
{
    [Fact]
    public void ItShouldReturnTheCanonicalLimitForCentimetres()
    {
        // Arrange
        // Act
        var limit = Sut.MaxDisplayLength(LengthUnitEnum.Cm);

        // Assert
        limit.Should().Be(CatchDetailConstants.MaxLengthCentimetres);
    }

    [Fact]
    public void ItShouldReturnALimitInInchesThatStillConvertsWithinTheCanonicalLimit()
    {
        // Arrange
        // Act
        var limit = Sut.MaxDisplayLength(LengthUnitEnum.In);

        // Assert
        limit.Should().Be(393.7m);
        var canonical = Sut.ToCanonicalLength(limit, LengthUnitEnum.In, null);
        CatchDetailConstants.IsLengthValid(canonical).Should().BeTrue();
    }
}
