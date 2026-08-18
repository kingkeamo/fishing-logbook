using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Catches.Commands.CreateCatchPhotographUploadCommandValidatorTests;

public class WhenTestingValidate : BaseCreateCatchPhotographUploadCommandValidatorTest
{
    [Fact]
    public void ItShouldRejectEmptyIds()
    {
        // Arrange
        var command = new CreateCatchPhotographUploadCommand
        {
            Request = new PhotographUploadRequestDto(Guid.Empty, "image/jpeg")
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CatchId);
        result.ShouldHaveValidationErrorFor(item => item.Request.PhotographId);
    }

    [Fact]
    public void ItShouldRejectAnUnsupportedContentType()
    {
        // Arrange
        var command = Command("image/gif");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Request.ContentType);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void ItShouldAcceptSupportedContentTypes(string contentType)
    {
        // Arrange
        var command = Command(contentType);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateCatchPhotographUploadCommand Command(string contentType)
    {
        return new CreateCatchPhotographUploadCommand
        {
            CatchId = Guid.NewGuid(),
            Request = new PhotographUploadRequestDto(Guid.NewGuid(), contentType)
        };
    }
}
