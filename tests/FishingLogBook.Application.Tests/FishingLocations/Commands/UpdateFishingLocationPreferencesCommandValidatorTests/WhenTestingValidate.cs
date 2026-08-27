using FishingLogBook.Application.FishingLocations.Commands;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.FishingLocations.Commands.UpdateFishingLocationPreferencesCommandValidatorTests;

public class WhenTestingValidate : BaseUpdateFishingLocationPreferencesCommandValidatorTest
{
    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var command = new UpdateFishingLocationPreferencesCommand
        {
            UserId = Guid.Empty,
            Locations = new UpdateFishingLocationPreferencesDto([])
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ItShouldRejectABlankLocationName(string name)
    {
        // Arrange
        var command = Command(Location(name));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Locations.Locations[0].Name");
    }

    [Fact]
    public void ItShouldRejectALocationNameLongerThanTheMaximum()
    {
        // Arrange
        var command = Command(Location(new string('a', FishingLocationConstants.MaxNameLength + 1)));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Locations.Locations[0].Name");
    }

    [Theory]
    [InlineData("Lough Corrib", "lough corrib")]
    [InlineData("Lough Corrib", " Lough Corrib ")]
    public void ItShouldRejectDuplicateLocationNames(string first, string second)
    {
        // Arrange
        var command = Command(Location(first), Location(second));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.Locations.Locations)
            .WithErrorMessage("A fishing location can only be saved once.");
    }

    [Fact]
    public void ItShouldRejectMoreThanOneDefaultLocation()
    {
        // Arrange
        var command = Command(Location("Lough Corrib", true), Location("River Moy", true));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.Locations.Locations)
            .WithErrorMessage("Only one fishing location can be the default.");
    }

    [Fact]
    public void ItShouldAcceptNoLocations()
    {
        // Arrange
        var command = Command();

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptSeveralLocationsWithNoDefault()
    {
        // Arrange
        var command = Command(Location("Lough Corrib"), Location("River Moy"));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptALocationNameOfExactlyTheMaximumLength()
    {
        // Arrange
        var command = Command(Location(new string('a', FishingLocationConstants.MaxNameLength)));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptSeveralLocationsWithOneDefault()
    {
        // Arrange
        var command = Command(
            Location("Lough Corrib", true),
            Location("Lough Mask"),
            Location("River Moy"));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
