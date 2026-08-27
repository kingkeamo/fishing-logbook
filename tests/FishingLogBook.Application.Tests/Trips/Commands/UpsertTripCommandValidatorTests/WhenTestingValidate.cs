using FishingLogBook.Shared.Constants;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Trips.Commands.UpsertTripCommandValidatorTests;

public class WhenTestingValidate : BaseUpsertTripCommandValidatorTest
{
    [Fact]
    public void ItShouldRejectAnEmptyOwner()
    {
        // Arrange
        var command = Command(userId: Guid.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.UserId);
    }

    [Fact]
    public void ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        var command = Command(tripId: Guid.Empty);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.Id);
    }

    [Theory]
    [InlineData("Planned")]
    [InlineData("Cancelled")]
    [InlineData("active")]
    [InlineData("")]
    public void ItShouldRejectAnUnsupportedStatus(string status)
    {
        // Arrange
        var command = Command(status: status);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.Status);
    }

    [Fact]
    public void ItShouldRejectAMissingStart()
    {
        // Arrange
        var command = Command(startedOn: default(DateTimeOffset));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.StartedOn);
    }

    [Fact]
    public void ItShouldRejectAStartInTheFuture()
    {
        // Arrange
        var command = Command(startedOn: DateTimeOffset.UtcNow.AddDays(1));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.StartedOn);
    }

    [Fact]
    public void ItShouldRejectAnActiveTripWithAnEnd()
    {
        // Arrange
        var command = Command(endedOn: StartedOn.AddHours(2));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip);
    }

    [Fact]
    public void ItShouldRejectAnEndBeforeTheStart()
    {
        // Arrange
        var command = Command(
            status: TripConstants.Completed,
            endedOn: StartedOn.AddSeconds(-1));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip);
    }

    [Fact]
    public void ItShouldRejectAnEndInTheFuture()
    {
        // Arrange
        var command = Command(
            status: TripConstants.Completed,
            endedOn: DateTimeOffset.UtcNow.AddDays(1));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip);
    }

    [Fact]
    public void ItShouldRejectATitleOverTheLimit()
    {
        // Arrange
        var command = Command(title: new string('a', TripConstants.MaxTitleLength + 1));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.Title);
    }

    [Fact]
    public void ItShouldRejectAPlaceNameOverTheLimit()
    {
        // Arrange
        var command = Command(placeName: new string('a', TripConstants.MaxPlaceNameLength + 1));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.PlaceName);
    }

    [Fact]
    public void ItShouldRejectAnUnsupportedLocationVisibility()
    {
        // Arrange
        var command = Command(location: Location(visibility: "Everyone"));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.Location!.Visibility);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    public void ItShouldRejectALatitudeOutsideTheAllowedRange(double latitude, double longitude)
    {
        // Arrange
        var command = Command(location: Location(latitude, longitude));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.Location!.Latitude);
    }

    [Theory]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void ItShouldRejectALongitudeOutsideTheAllowedRange(double latitude, double longitude)
    {
        // Arrange
        var command = Command(location: Location(latitude, longitude));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.Location!.Longitude);
    }

    [Fact]
    public void ItShouldRejectALocationWithNoSource()
    {
        // Arrange
        var command = Command(location: Location(source: string.Empty));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.Location!.Source);
    }

    [Fact]
    public void ItShouldRejectALocationWithNoConsentVersion()
    {
        // Arrange
        var command = Command(location: Location(consentVersion: string.Empty));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Trip.Location!.ConsentVersion);
    }

    [Fact]
    public void ItShouldAcceptATitleAtTheLimit()
    {
        // Arrange
        var command = Command(title: new string('a', TripConstants.MaxTitleLength));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptAPlaceNameAtTheLimit()
    {
        // Arrange
        var command = Command(placeName: new string('a', TripConstants.MaxPlaceNameLength));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Private")]
    [InlineData("Approximate")]
    [InlineData("FishingVenueOnly")]
    [InlineData("Public")]
    public void ItShouldAcceptEverySupportedLocationVisibility(string visibility)
    {
        // Arrange
        var command = Command(location: Location(visibility: visibility));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptAHistoricalCompletedTrip()
    {
        // Arrange
        var command = Command(
            status: TripConstants.Completed,
            startedOn: DateTimeOffset.Parse("2019-06-14T05:32:00Z"),
            endedOn: DateTimeOffset.Parse("2019-06-14T16:22:00Z"));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptACompletedTripWithNoEnd()
    {
        // Arrange
        var command = Command(status: TripConstants.Completed);

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptABlankActiveTrip()
    {
        // Arrange
        var command = Command();

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
