using FishingLogBook.Application.Trips.Commands;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.DeleteTripNoteCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly DeleteTripNoteCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = new DeleteTripNoteCommand { TripId = Guid.Empty, NoteId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
        result.ShouldNotHaveValidationErrorFor(item => item.NoteId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyNoteId()
    {
        // Arrange
        var command = new DeleteTripNoteCommand { TripId = Guid.NewGuid(), NoteId = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.NoteId);
        result.ShouldNotHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldAcceptACompleteCommand()
    {
        // Arrange
        var command = new DeleteTripNoteCommand { TripId = Guid.NewGuid(), NoteId = Guid.NewGuid() };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
