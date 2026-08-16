using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
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
    public void ItShouldHaveAValidationErrorWhenLocationVisibilityIsUnknown()
    {
        // Arrange
        var command = new UpdateOwnProfileCommand
        {
            UserId = Guid.NewGuid(),
            Profile = ValidProfile() with
            {
                Location = new CatchLocationDto(
                    53.4,
                    -7.9,
                    12,
                    DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                    LocationDefaults.DeviceGps,
                    "Club",
                    LocationDefaults.ConsentVersion)
            }
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Profile.Location!.Visibility)
            .WithErrorMessage("Location visibility is not recognised.");
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
            false,
            new CatchLocationDto(
                53.4,
                -7.9,
                12,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));
    }
}
