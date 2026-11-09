using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.CreateTripPhotographUploadCommandValidatorTests;

public class WhenTestingValidate
{
    private readonly CreateTripPhotographUploadCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = Command(Guid.Empty, Guid.NewGuid(), "image/jpeg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyPhotographId()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.Empty, "image/jpeg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Request.PhotographId);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("")]
    [InlineData("text/plain")]
    public void ItShouldRejectAnUnsupportedContentType(string contentType)
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), contentType);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Request.ContentType);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void ItShouldAcceptEverySupportedContentType(string contentType)
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), contentType);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateTripPhotographUploadCommand Command(Guid tripId, Guid photographId, string contentType)
    {
        return new CreateTripPhotographUploadCommand
        {
            TripId = tripId,
            Request = new PhotographUploadRequestDto(photographId, contentType)
        };
    }
}
