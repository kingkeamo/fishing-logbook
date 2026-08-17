using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Catches.Commands.UpsertCatchCommandValidatorTests;

public class WhenTestingValidate : BaseUpsertCatchCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var command = Command(userId: Guid.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenCatchIdIsEmpty()
    {
        // Arrange
        var command = Command(catchId: Guid.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Id);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenCaughtOnIsMissing()
    {
        // Arrange
        var command = Command(useDefaultCaughtOn: true);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.CaughtOn);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenPhotographsAreMissing()
    {
        // Arrange
        var command = Command(photographs: []);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Photographs)
            .WithErrorMessage("A catch requires at least one photograph.");
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenPhotographIdIsEmpty()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var command = Command(
            catchId: catchId,
            photographs: [new CatchPhotographDto(Guid.Empty, catchId, PhotographContentTypeConstants.Jpeg)]);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Catch.Photographs[0].Id");
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenPhotographCatchIdDoesNotMatch()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var command = Command(
            catchId: catchId,
            photographs: [new CatchPhotographDto(Guid.NewGuid(), Guid.NewGuid(), PhotographContentTypeConstants.Jpeg)]);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Photographs)
            .WithErrorMessage("Each photograph must belong to the catch.");
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    [InlineData("")]
    public void ItShouldHaveAValidationErrorWhenContentTypeIsNotAllowed(string contentType)
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var command = Command(
            catchId: catchId,
            photographs: [new CatchPhotographDto(Guid.NewGuid(), catchId, contentType)]);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Catch.Photographs[0].ContentType")
            .WithErrorMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
    }

    [Theory]
    [InlineData(PhotographContentTypeConstants.Jpeg)]
    [InlineData(PhotographContentTypeConstants.Png)]
    [InlineData(PhotographContentTypeConstants.Webp)]
    public void ItShouldAcceptEveryAllowedContentType(string contentType)
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var command = Command(
            catchId: catchId,
            photographs: [new CatchPhotographDto(Guid.NewGuid(), catchId, contentType)]);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = Command();

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static UpsertCatchCommand Command(
        Guid? userId = null,
        Guid? catchId = null,
        DateTimeOffset caughtOn = new(),
        bool useDefaultCaughtOn = false,
        IReadOnlyList<CatchPhotographDto>? photographs = null)
    {
        var resolvedCatchId = catchId ?? Guid.NewGuid();
        return new UpsertCatchCommand
        {
            UserId = userId ?? Guid.NewGuid(),
            Catch = new CatchDto(
                resolvedCatchId,
                useDefaultCaughtOn ? default : caughtOn == default
                    ? DateTimeOffset.Parse("2026-08-17T08:00:00Z")
                    : caughtOn,
                photographs ??
                [
                    new CatchPhotographDto(Guid.NewGuid(), resolvedCatchId, PhotographContentTypeConstants.Jpeg)
                ])
        };
    }
}
