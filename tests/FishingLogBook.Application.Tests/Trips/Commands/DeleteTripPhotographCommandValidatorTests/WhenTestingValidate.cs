using FishingLogBook.Application.Trips.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.DeleteTripPhotographCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly DeleteTripPhotographCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = new DeleteTripPhotographCommand { TripId = Guid.Empty, PhotographId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
        result.ShouldNotHaveValidationErrorFor(item => item.PhotographId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyPhotographId()
    {
        // Arrange
        var command = new DeleteTripPhotographCommand { TripId = Guid.NewGuid(), PhotographId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.PhotographId);
        result.ShouldNotHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldAcceptACompleteCommand()
    {
        // Arrange
        var command = new DeleteTripPhotographCommand { TripId = Guid.NewGuid(), PhotographId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
