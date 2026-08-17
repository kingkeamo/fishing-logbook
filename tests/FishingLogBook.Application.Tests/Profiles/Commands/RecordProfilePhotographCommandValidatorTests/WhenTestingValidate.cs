using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Commands.RecordProfilePhotographCommandValidatorTests;

public class WhenTestingValidate : BaseRecordProfilePhotographCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var command = Command(Guid.Empty, Guid.NewGuid(), "profiles/key", PhotographContentTypeConstants.Jpeg);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenPhotographIdIsEmpty()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.Empty, "profiles/key", PhotographContentTypeConstants.Jpeg);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Photograph.PhotographId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenObjectKeyIsMissing()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "  ", PhotographContentTypeConstants.Jpeg);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Photograph.ObjectKey);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("image/gif")]
    [InlineData("image/heic")]
    [InlineData("")]
    public void ItShouldHaveAValidationErrorWhenContentTypeIsNotAllowed(string contentType)
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "profiles/key", contentType);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Photograph.ContentType)
            .WithErrorMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
    }

    [Theory]
    [InlineData(PhotographContentTypeConstants.Jpeg)]
    [InlineData(PhotographContentTypeConstants.Png)]
    [InlineData(PhotographContentTypeConstants.Webp)]
    public void ItShouldAcceptEveryAllowedContentType(string contentType)
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "profiles/key", contentType);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "profiles/key", PhotographContentTypeConstants.Png);

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
