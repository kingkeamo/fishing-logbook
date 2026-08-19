using AwesomeAssertions;
using FishingLogBook.Application.FishingPreferences.Commands;
using FishingLogBook.Application.FishingPreferences.Errors;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Commands.UpdateFishingPreferencesCommandTests;

public class WhenTestingHandle : BaseUpdateFishingPreferencesCommandTest
{
    [Fact]
    public async Task ItShouldReturnTheErrorMessageWhenTheServiceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = ValidCommand(userId);
        MockFishingPreferenceService
            .UpdatePreferencesAsync(userId, command.Preferences, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<FishingPreferencesDto>("Failed to save fishing preferences."));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to save fishing preferences.");
        response.Preferences.Should().BeNull();
        await MockFishingPreferenceService.Received(1).UpdatePreferencesAsync(
            userId,
            command.Preferences,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveTheTypedErrorWhenTheCatalogueEntryIsUnknown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = ValidCommand(userId);
        MockFishingPreferenceService
            .UpdatePreferencesAsync(userId, command.Preferences, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<FishingPreferencesDto>(
                new UnknownFishingCatalogueEntryError("One or more species are not recognised.")));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<UnknownFishingCatalogueEntryError>();
        response.ErrorMessage.Should().Be("One or more species are not recognised.");
        await MockFishingPreferenceService.Received(1).UpdatePreferencesAsync(
            userId,
            command.Preferences,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheSavedPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = ValidCommand(userId);
        var saved = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(FlyMethodId, "Fly", "Fly", true, [])
        ]);
        MockFishingPreferenceService
            .UpdatePreferencesAsync(userId, command.Preferences, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(saved));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Preferences.Should().BeSameAs(saved);
        await MockFishingPreferenceService.Received(1).UpdatePreferencesAsync(
            userId,
            Arg.Is<UpdateFishingPreferencesDto>(preferences =>
                preferences.Methods.Count == 1
                && preferences.Methods[0].FishingMethodId == FlyMethodId
                && preferences.Methods[0].IsDefault),
            Arg.Any<CancellationToken>());
        await MockFishingPreferenceService.DidNotReceive().GetPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    private static UpdateFishingPreferencesCommand ValidCommand(Guid userId)
    {
        return new UpdateFishingPreferencesCommand
        {
            UserId = userId,
            Preferences = new UpdateFishingPreferencesDto(
            [
                new UpdateFishingMethodPreferenceDto(
                    FlyMethodId,
                    true,
                    [new UpdateFishingSpeciesPreferenceDto(BrownTroutSpeciesId, true)])
            ])
        };
    }
}
