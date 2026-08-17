using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Commands.CreateProfilePhotographUploadCommandValidatorTests;

public class WhenTestingValidate : BaseCreateProfilePhotographUploadCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var command = ValidCommand(Guid.Empty, Guid.NewGuid(), PhotographContentTypeConstants.Jpeg);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenPhotographIdIsEmpty()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid(), Guid.Empty, PhotographContentTypeConstants.Jpeg);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Request.PhotographId);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("image/gif")]
    [InlineData("image/heic")]
    [InlineData("image/")]
    [InlineData("")]
    public void ItShouldHaveAValidationErrorWhenContentTypeIsNotAllowed(string contentType)
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid(), Guid.NewGuid(), contentType);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Request.ContentType)
            .WithErrorMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
    }

    [Theory]
    [InlineData(PhotographContentTypeConstants.Jpeg)]
    [InlineData(PhotographContentTypeConstants.Png)]
    [InlineData(PhotographContentTypeConstants.Webp)]
    public void ItShouldAcceptEveryAllowedContentType(string contentType)
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid(), Guid.NewGuid(), contentType);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid(), Guid.NewGuid(), PhotographContentTypeConstants.Jpeg);

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
