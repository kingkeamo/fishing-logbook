using FishingLogBook.Application.FishingPreferences.Commands;
using FishingLogBook.Shared.Dtos;
using FluentValidation.TestHelper;

namespace FishingLogBook.Application.Tests.FishingPreferences.Commands.UpdateFishingPreferencesCommandValidatorTests;

public class WhenTestingValidate : BaseUpdateFishingPreferencesCommandValidatorTest
{
    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var command = new UpdateFishingPreferencesCommand
        {
            UserId = Guid.Empty,
            Preferences = new UpdateFishingPreferencesDto([])
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.UserId);
    }

    [Fact]
    public void ItShouldRejectANullPreferencesBody()
    {
        // Arrange
        var command = new UpdateFishingPreferencesCommand
        {
            UserId = Guid.NewGuid(),
            Preferences = null!
        };

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.Preferences);
    }

    [Fact]
    public void ItShouldRejectADuplicateFishingMethod()
    {
        // Arrange
        var command = Command(
            new UpdateFishingMethodPreferenceDto(FlyMethodId, true, []),
            new UpdateFishingMethodPreferenceDto(FlyMethodId, false, []));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Preferences.Methods")
            .WithErrorMessage("A fishing method can only be selected once.");
    }

    [Fact]
    public void ItShouldRejectMoreThanOneDefaultMethod()
    {
        // Arrange
        var command = Command(
            new UpdateFishingMethodPreferenceDto(FlyMethodId, true, []),
            new UpdateFishingMethodPreferenceDto(SpinningMethodId, true, []));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Preferences.Methods")
            .WithErrorMessage("Only one fishing method can be the default.");
    }

    [Fact]
    public void ItShouldRejectAnEmptyFishingMethodId()
    {
        // Arrange
        var command = Command(new UpdateFishingMethodPreferenceDto(Guid.Empty, true, []));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Preferences.Methods[0].FishingMethodId");
    }

    [Fact]
    public void ItShouldRejectADuplicateSpeciesWithinAMethod()
    {
        // Arrange
        var command = Command(new UpdateFishingMethodPreferenceDto(
            FlyMethodId,
            true,
            [
                new UpdateFishingSpeciesPreferenceDto(BrownTroutSpeciesId, true),
                new UpdateFishingSpeciesPreferenceDto(BrownTroutSpeciesId, false)
            ]));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Preferences.Methods[0].Species")
            .WithErrorMessage("A species can only be selected once for a fishing method.");
    }

    [Fact]
    public void ItShouldRejectMoreThanOneDefaultSpeciesWithinAMethod()
    {
        // Arrange
        var command = Command(new UpdateFishingMethodPreferenceDto(
            FlyMethodId,
            true,
            [
                new UpdateFishingSpeciesPreferenceDto(BrownTroutSpeciesId, true),
                new UpdateFishingSpeciesPreferenceDto(PikeSpeciesId, true)
            ]));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Preferences.Methods[0].Species")
            .WithErrorMessage("Only one species can be the default for a fishing method.");
    }

    [Fact]
    public void ItShouldRejectAnEmptySpeciesId()
    {
        // Arrange
        var command = Command(new UpdateFishingMethodPreferenceDto(
            FlyMethodId,
            true,
            [new UpdateFishingSpeciesPreferenceDto(Guid.Empty, false)]));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Preferences.Methods[0].Species[0].SpeciesId");
    }

    [Fact]
    public void ItShouldAcceptAnEmptySelection()
    {
        // Arrange
        var command = Command();

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ItShouldAcceptSeveralMethodsWithOneDefaultEach()
    {
        // Arrange
        var command = Command(
            new UpdateFishingMethodPreferenceDto(
                FlyMethodId,
                true,
                [
                    new UpdateFishingSpeciesPreferenceDto(BrownTroutSpeciesId, true),
                    new UpdateFishingSpeciesPreferenceDto(PikeSpeciesId, false)
                ]),
            new UpdateFishingMethodPreferenceDto(
                SpinningMethodId,
                false,
                [new UpdateFishingSpeciesPreferenceDto(PikeSpeciesId, true)]));

        // Act
        var result = Sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static UpdateFishingPreferencesCommand Command(params UpdateFishingMethodPreferenceDto[] methods)
    {
        return new UpdateFishingPreferencesCommand
        {
            UserId = Guid.NewGuid(),
            Preferences = new UpdateFishingPreferencesDto(methods)
        };
    }
}
