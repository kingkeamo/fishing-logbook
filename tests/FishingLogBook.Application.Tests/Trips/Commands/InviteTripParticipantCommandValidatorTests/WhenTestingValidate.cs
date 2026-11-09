using FishingLogBook.Application.Trips.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.InviteTripParticipantCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly InviteTripParticipantCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = new InviteTripParticipantCommand { TripId = Guid.Empty, InvitedUserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
        result.ShouldNotHaveValidationErrorFor(item => item.InvitedUserId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyInvitedUserId()
    {
        // Arrange
        var command = new InviteTripParticipantCommand { TripId = Guid.NewGuid(), InvitedUserId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.InvitedUserId);
        result.ShouldNotHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldAcceptACompleteCommand()
    {
        // Arrange
        var command = new InviteTripParticipantCommand { TripId = Guid.NewGuid(), InvitedUserId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
