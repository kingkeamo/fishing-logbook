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
    public void ItShouldHaveAValidationErrorWhenFishingTypeIsUnknown()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { PreferredFishingTypes = ["NotAType"] }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Profile.PreferredFishingTypes[0]");
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenPreferredSpeciesIsBlank()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { PreferredSpecies = ["   "] }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Profile.PreferredSpecies[0]");
    }

    [Fact]
    public void ItShouldHaveAValidationErrorWhenPreferredSpeciesIsTooLong()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { PreferredSpecies = [new string('P', 51)] }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Profile.PreferredSpecies[0]");
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
    public void ItShouldAcceptAPreferredSpeciesAtTheMaximumLength()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { PreferredSpecies = [new string('P', 50)] }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(nameof(FishingTypeEnum.Coarse))]
    [InlineData(nameof(FishingTypeEnum.Game))]
    [InlineData(nameof(FishingTypeEnum.Sea))]
    [InlineData(nameof(FishingTypeEnum.Fly))]
    [InlineData(nameof(FishingTypeEnum.Lure))]
    [InlineData(nameof(FishingTypeEnum.Match))]
    [InlineData(nameof(FishingTypeEnum.Predator))]
    public void ItShouldAcceptEverySupportedFishingType(string fishingType)
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with { PreferredFishingTypes = [fishingType] }
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

    private static UpdateProfileDto ValidProfile()
    {
        return new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            ["Coarse", "Fly"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false);
    }
}
