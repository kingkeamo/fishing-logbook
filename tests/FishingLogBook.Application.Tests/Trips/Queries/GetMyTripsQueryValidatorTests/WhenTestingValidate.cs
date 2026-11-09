using FishingLogBook.Application.Trips.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Queries.GetMyTripsQueryValidatorTests;

public class WhenTestingValidate
{
    private readonly GetMyTripsQueryValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var command = new GetMyTripsQuery { UserId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.UserId);
    }

    [Fact]
    public void ItShouldAcceptAUserId()
    {
        // Arrange
        var command = new GetMyTripsQuery { UserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
