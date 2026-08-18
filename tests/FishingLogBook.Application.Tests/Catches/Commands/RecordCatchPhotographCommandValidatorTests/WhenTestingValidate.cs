using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Catches.Commands.RecordCatchPhotographCommandValidatorTests;

public class WhenTestingValidate : BaseRecordCatchPhotographCommandValidatorTest
{
    [Fact]
    public void ItShouldRejectEmptyRequiredValues()
    {
        // Arrange
        var command = new RecordCatchPhotographCommand
        {
            Photograph = new RecordPhotographDto(Guid.Empty, string.Empty, "image/jpeg")
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.CatchId);
        result.ShouldHaveValidationErrorFor(item => item.Photograph.PhotographId);
        result.ShouldHaveValidationErrorFor(item => item.Photograph.ObjectKey);
    }

    [Fact]
    public void ItShouldRejectAnUnsupportedContentType()
    {
        // Arrange
        var command = Command("image/gif");

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Photograph.ContentType);
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

    private static RecordCatchPhotographCommand Command(string contentType)
    {
        return new RecordCatchPhotographCommand
        {
            CatchId = Guid.NewGuid(),
            Photograph = new RecordPhotographDto(
                Guid.NewGuid(),
                "object-key",
                contentType)
        };
    }
}
