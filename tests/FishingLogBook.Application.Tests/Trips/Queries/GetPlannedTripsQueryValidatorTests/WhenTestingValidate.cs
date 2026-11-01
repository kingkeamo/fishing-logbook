using FishingLogBook.Application.Trips.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetPlannedTripsQueryValidatorTests;

public class WhenTestingValidate
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly GetPlannedTripsQueryValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyUser()
    {
        // Arrange
        var query = new GetPlannedTripsQuery { UserId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.UserId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void ItShouldRejectAWindowOutsideZeroToThirtyDays(int windowDays)
    {
        // Arrange
        var query = new GetPlannedTripsQuery { UserId = UserId, WindowDays = windowDays };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.WindowDays);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(30)]
    public void ItShouldAcceptNoWindowOrOneWithinRange(int? windowDays)
    {
        // Arrange
        var query = new GetPlannedTripsQuery { UserId = UserId, WindowDays = windowDays };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
