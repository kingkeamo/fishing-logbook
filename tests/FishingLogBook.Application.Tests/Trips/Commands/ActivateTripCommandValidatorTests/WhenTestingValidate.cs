using FishingLogBook.Application.Trips.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.ActivateTripCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly ActivateTripCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = new ActivateTripCommand { TripId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldAcceptATripId()
    {
        // Arrange
        var command = new ActivateTripCommand { TripId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
