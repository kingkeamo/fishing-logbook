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

    [Theory]
    [InlineData(91)]
    [InlineData(-91)]
    public void ItShouldHaveAValidationErrorWhenLatitudeIsOutOfRange(double latitude)
    {
        // Arrange
        var command = Command(location: ValidLocation() with { Latitude = latitude });

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Location!.Latitude);
    }

    [Theory]
    [InlineData(181)]
    [InlineData(-181)]
    public void ItShouldHaveAValidationErrorWhenLongitudeIsOutOfRange(double longitude)
    {
        // Arrange
        var command = Command(location: ValidLocation() with { Longitude = longitude });

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Location!.Longitude);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenLocationCapturedOnIsMissing()
    {
        // Arrange
        var command = Command(location: ValidLocation() with { CapturedOn = default });

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Location!.CapturedOn);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ItShouldHaveAValidationErrorWhenLocationSourceIsMissing(string source)
    {
        // Arrange
        var command = Command(location: ValidLocation() with { Source = source });

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Location!.Source);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenLocationVisibilityIsUnknown()
    {
        // Arrange
        var command = Command(location: ValidLocation() with { Visibility = "FriendsOnly" });

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Location!.Visibility)
            .WithErrorMessage("Location visibility is not supported.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ItShouldHaveAValidationErrorWhenLocationVisibilityIsMissing(string visibility)
    {
        // Arrange
        var command = Command(location: ValidLocation() with { Visibility = visibility });

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Location!.Visibility);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ItShouldHaveAValidationErrorWhenLocationConsentVersionIsMissing(string consentVersion)
    {
        // Arrange
        var command = Command(location: ValidLocation() with { ConsentVersion = consentVersion });

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Location!.ConsentVersion);
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void ItShouldAcceptBoundaryCoordinates(double latitude, double longitude)
    {
        // Arrange
        var command = Command(location: ValidLocation() with { Latitude = latitude, Longitude = longitude });

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

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidLocatedCommand()
    {
        // Arrange
        var command = Command(location: ValidLocation());

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000.001)]
    public void ItShouldHaveAValidationErrorWhenWeightIsOutOfRange(double weight)
    {
        // Arrange
        var command = Command(weight: (decimal)weight);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Weight!.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000.001)]
    public void ItShouldHaveAValidationErrorWhenLengthIsOutOfRange(double length)
    {
        // Arrange
        var command = Command(length: (decimal)length);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.Length!.Value);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenCaughtOnIsInTheFuture()
    {
        // Arrange
        var command = Command(caughtOn: DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.CaughtOn)
            .WithErrorMessage("Catch time cannot be in the future.");
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenSpeciesNameIsTooLong()
    {
        // Arrange
        var command = Command(speciesName: new string('a', CatchDetailConstants.MaxSpeciesNameLength + 1));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Catch.SpeciesName);
    }

    [Fact]
    public void ItShouldAcceptBoundaryMeasurementsAndOptionalDetails()
    {
        // Arrange
        var command = Command(
            speciesName: new string('a', CatchDetailConstants.MaxSpeciesNameLength),
            weight: CatchDetailConstants.MaxWeightKilograms,
            length: CatchDetailConstants.MaxLengthCentimetres,
            method: new string('m', CatchDetailConstants.MaxMethodLength),
            baitOrLure: new string('b', CatchDetailConstants.MaxBaitOrLureLength),
            notes: new string('n', CatchDetailConstants.MaxNotesLength));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CatchLocationDto ValidLocation()
    {
        return new CatchLocationDto(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }

    private static UpsertCatchCommand Command(
        Guid? userId = null,
        Guid? catchId = null,
        DateTimeOffset caughtOn = new(),
        bool useDefaultCaughtOn = false,
        IReadOnlyList<CatchPhotographDto>? photographs = null,
        CatchLocationDto? location = null,
        string? speciesName = null,
        decimal? weight = null,
        decimal? length = null,
        string? method = null,
        string? baitOrLure = null,
        string? notes = null)
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
                ],
                location)
            {
                SpeciesName = speciesName,
                Weight = weight,
                Length = length,
                Method = method,
                BaitOrLure = baitOrLure,
                Notes = notes
            }
        };
    }
}
