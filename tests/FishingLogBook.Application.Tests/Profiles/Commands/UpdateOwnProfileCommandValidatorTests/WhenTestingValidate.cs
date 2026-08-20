using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.Profiles.Commands.UpdateOwnProfileCommandValidatorTests;

public class WhenTestingValidate : BaseUpdateOwnProfileCommandValidatorTest
{
    [Fact]
    public void ItShouldHaveAValidationErrorWhenUserIdIsEmpty()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.Empty,
            Profile = ValidProfile()
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenDisplayNameIsTooLong()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { DisplayName = new string('A', 101) }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Profile.DisplayName);
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenHomeRegionIsTooLong()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { HomeRegion = new string('B', 201) }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Profile.HomeRegion);
    }

    [Fact]
    public void ItShouldAcceptADisplayNameAtTheMaximumLength()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { DisplayName = new string('A', 100) }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptAHomeRegionAtTheMaximumLength()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { HomeRegion = new string('B', 200) }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldNotHaveValidationErrorsForAValidCommand()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile()
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenThePreferredWeightUnitIsNotAKnownValue()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { PreferredWeightUnit = (WeightUnitEnum)7 }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.Profile.PreferredWeightUnit)
            .WithErrorMessage("Preferred weight unit is not recognised.");
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenThePreferredLengthUnitIsNotAKnownValue()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { PreferredLengthUnit = (LengthUnitEnum)7 }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.Profile.PreferredLengthUnit)
            .WithErrorMessage("Preferred length unit is not recognised.");
    }

    [Theory]
    [InlineData(WeightUnitEnum.Kg, LengthUnitEnum.Cm)]
    [InlineData(WeightUnitEnum.Lb, LengthUnitEnum.In)]
    [InlineData(WeightUnitEnum.Kg, LengthUnitEnum.In)]
    [InlineData(WeightUnitEnum.Lb, LengthUnitEnum.Cm)]
    public void ItShouldAcceptEverySupportedUnitCombination(WeightUnitEnum weightUnit, LengthUnitEnum lengthUnit)
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with
            {
                PreferredWeightUnit = weightUnit,
                PreferredLengthUnit = lengthUnit
            }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static UpdateProfileDto ValidProfile()
    {
        return new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            true,
            false,
            true,
            true,
            false);
    }
}
