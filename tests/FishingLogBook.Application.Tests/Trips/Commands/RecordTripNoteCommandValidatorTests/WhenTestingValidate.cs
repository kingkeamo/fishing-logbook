using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.RecordTripNoteCommandValidatorTests;

public class WhenTestingValidate : BaseRecordTripNoteCommandValidatorTest
{
    [Fact]
    public void ItShouldRejectAMissingTrip()
    {
        // Arrange
        var command = Command(tripId: Guid.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldRejectAMissingNoteIdentifier()
    {
        // Arrange
        var command = Command(noteId: Guid.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Note.NoteId);
    }

    [Fact]
    public void ItShouldRejectAMissingRecordedInstant()
    {
        // Arrange
        var command = Command(recordedOn: default(DateTimeOffset));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Note.RecordedOn);
    }

    [Fact]
    public void ItShouldRejectEmptyText()
    {
        // Arrange
        var command = Command(text: string.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Note.Text);
    }

    [Fact]
    public void ItShouldRejectWhitespaceOnlyText()
    {
        // Arrange
        var command = Command(text: "   \t  ");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Note.Text);
    }

    [Fact]
    public void ItShouldRejectTextOneCharacterOverTheCap()
    {
        // Arrange
        var command = Command(text: new string('a', TripConstants.MaxNoteTextLength + 1));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Note.Text);
    }

    [Fact]
    public void ItShouldAcceptTextAtExactlyTheCap()
    {
        // Arrange
        var command = Command(text: new string('a', TripConstants.MaxNoteTextLength));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptAnOrdinaryNote()
    {
        // Arrange
        var command = Command();

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static RecordTripNoteCommand Command(
        string text = "water dropped about a foot",
        Guid? noteId = null,
        DateTimeOffset? recordedOn = null,
        Guid? tripId = null)
    {
        return new RecordTripNoteCommand
        {
            TripId = tripId ?? TripId,
            Note = new RecordTripNoteDto(
                noteId ?? NoteId,
                text,
                recordedOn ?? RecordedOn)
        };
    }
}
