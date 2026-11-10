using FishingLogBook.Application.LocationLookup.Queries;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.LocationLookup.Queries.GetLocationLabelQueryValidatorTests;

public class WhenTestingValidate
{
    private readonly GetLocationLabelQueryValidator _validator = new();

    [Theory]
    [InlineData(-90.0001)]
    [InlineData(90.0001)]
    public void ItShouldRejectALatitudeOutsideTheValidRange(double latitude)
    {
        // Arrange
        var query = new GetLocationLabelQuery { Latitude = latitude, Longitude = 0 };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Latitude);
        result.ShouldNotHaveValidationErrorFor(item => item.Longitude);
    }

    [Theory]
    [InlineData(-180.0001)]
    [InlineData(180.0001)]
    public void ItShouldRejectALongitudeOutsideTheValidRange(double longitude)
    {
        // Arrange
        var query = new GetLocationLabelQuery { Latitude = 0, Longitude = longitude };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(item => item.Longitude);
        result.ShouldNotHaveValidationErrorFor(item => item.Latitude);
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void ItShouldAcceptCoordinatesWithinTheValidRange(double latitude, double longitude)
    {
        // Arrange
        var query = new GetLocationLabelQuery { Latitude = latitude, Longitude = longitude };

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
