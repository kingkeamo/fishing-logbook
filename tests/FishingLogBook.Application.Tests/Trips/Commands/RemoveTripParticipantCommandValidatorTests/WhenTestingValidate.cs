using FishingLogBook.Application.Trips.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.RemoveTripParticipantCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly RemoveTripParticipantCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = new RemoveTripParticipantCommand { TripId = Guid.Empty, ParticipantUserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
        result.ShouldNotHaveValidationErrorFor(item => item.ParticipantUserId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyParticipantUserId()
    {
        // Arrange
        var command = new RemoveTripParticipantCommand { TripId = Guid.NewGuid(), ParticipantUserId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.ParticipantUserId);
        result.ShouldNotHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldAcceptACompleteCommand()
    {
        // Arrange
        var command = new RemoveTripParticipantCommand { TripId = Guid.NewGuid(), ParticipantUserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
