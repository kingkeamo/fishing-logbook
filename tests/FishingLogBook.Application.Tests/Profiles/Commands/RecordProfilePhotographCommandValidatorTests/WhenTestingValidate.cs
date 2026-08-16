using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Commands.RecordProfilePhotographCommandValidatorTests;

public class WhenTestingValidate : BaseRecordProfilePhotographCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var command = Command(Guid.Empty, Guid.NewGuid(), "profiles/key", "image/jpeg");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenObjectKeyIsMissing()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "  ", "image/jpeg");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Photograph.ObjectKey);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenContentTypeIsNotAnImage()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "profiles/key", "text/plain");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Photograph.ContentType)
            .WithErrorMessage("Photograph content type must be an image.");
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "profiles/key", "image/png");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static RecordProfilePhotographCommand Command(
        Guid userId,
        Guid photographId,
        string objectKey,
        string contentType)
    {
        return new RecordProfilePhotographCommand
        {
            UserId = userId,
            Photograph = new RecordPhotographDto(photographId, objectKey, contentType)
        };
    }
}
