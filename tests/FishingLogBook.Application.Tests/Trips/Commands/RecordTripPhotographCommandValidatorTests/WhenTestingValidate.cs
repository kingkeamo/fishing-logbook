using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.RecordTripPhotographCommandValidatorTests;

public class WhenTestingValidate
{
    private static readonly DateTimeOffset AddedOn = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    private readonly RecordTripPhotographCommandValidator _validator = new();

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = Command(Guid.Empty, Guid.NewGuid(), "key.jpg", AddedOn, "image/jpeg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.TripId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyPhotographId()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.Empty, "key.jpg", AddedOn, "image/jpeg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Photograph.PhotographId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ItShouldRejectABlankObjectKey(string objectKey)
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), objectKey, AddedOn, "image/jpeg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Photograph.ObjectKey);
    }

    [Fact]
    public void ItShouldRejectAMissingAddedOnTime()
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "key.jpg", default, "image/jpeg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Photograph.AddedOn);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("")]
    [InlineData("application/pdf")]
    public void ItShouldRejectAnUnsupportedContentType(string contentType)
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "key.jpg", AddedOn, contentType);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Photograph.ContentType);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void ItShouldAcceptEverySupportedContentType(string contentType)
    {
        // Arrange
        var command = Command(Guid.NewGuid(), Guid.NewGuid(), "key.jpg", AddedOn, contentType);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static RecordTripPhotographCommand Command(
        Guid tripId,
        Guid photographId,
        string objectKey,
        DateTimeOffset addedOn,
        string contentType)
    {
        return new RecordTripPhotographCommand
        {
            TripId = tripId,
            Photograph = new RecordTripPhotographDto(photographId, objectKey, contentType, addedOn)
        };
    }
}
