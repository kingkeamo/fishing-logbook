using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Commands.CreateProfilePhotographUploadCommandValidatorTests;

public class WhenTestingValidate : BaseCreateProfilePhotographUploadCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var command = ValidCommand(Guid.Empty, Guid.NewGuid(), "image/jpeg");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenPhotographIdIsEmpty()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid(), Guid.Empty, "image/jpeg");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Request.PhotographId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenContentTypeIsNotAnImage()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid(), Guid.NewGuid(), "application/pdf");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Request.ContentType)
            .WithErrorMessage("Photograph content type must be an image.");
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid(), Guid.NewGuid(), "image/jpeg");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateProfilePhotographUploadCommand ValidCommand(
        Guid userId,
        Guid photographId,
        string contentType)
    {
        return new CreateProfilePhotographUploadCommand
        {
            UserId = userId,
            Request = new PhotographUploadRequestDto(photographId, contentType)
        };
    }
}
