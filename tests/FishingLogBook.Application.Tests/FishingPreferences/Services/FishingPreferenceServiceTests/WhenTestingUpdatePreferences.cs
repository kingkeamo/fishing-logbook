using AwesomeAssertions;
using FishingLogBook.Application.FishingPreferences.Errors;
using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Services.FishingPreferenceServiceTests;

public class WhenTestingUpdatePreferences : BaseFishingPreferenceServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheCatalogueCannotBeLoaded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockFishingCatalogueRepository
            .GetAllMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<FishingMethod>>("Failed to load fishing method catalogue."));

        // Act
        var result = await Sut.UpdatePreferencesAsync(userId, ValidUpdate(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load fishing method catalogue.");
        await MockFishingPreferenceRepository.DidNotReceive().ReplacePreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
            Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenAMethodIsNotInTheCatalogue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        var update = new UpdateFishingPreferencesDto(
            [new UpdateFishingMethodPreferenceDto(UnknownId, true, [])]);

        // Act
        var result = await Sut.UpdatePreferencesAsync(userId, update, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<UnknownFishingCatalogueEntryError>();
        result.Errors[0].Message.Should().Be("One or more fishing methods are not recognised.");
        await MockFishingPreferenceRepository.DidNotReceive().ReplacePreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
            Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenASpeciesIsNotInTheCatalogue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        var update = new UpdateFishingPreferencesDto(
        [
            new UpdateFishingMethodPreferenceDto(
                FlyMethodId,
                true,
                [new UpdateFishingSpeciesPreferenceDto(UnknownId, true)])
        ]);

        // Act
        var result = await Sut.UpdatePreferencesAsync(userId, update, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<UnknownFishingCatalogueEntryError>();
        result.Errors[0].Message.Should().Be("One or more species are not recognised.");
        await MockFishingPreferenceRepository.DidNotReceive().ReplacePreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
            Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheReplaceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        MockFishingPreferenceRepository
            .ReplacePreferencesAsync(
                userId,
                Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
                Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Failed to save fishing preferences."));

        // Act
        var result = await Sut.UpdatePreferencesAsync(userId, ValidUpdate(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save fishing preferences.");
        await MockFishingPreferenceRepository.DidNotReceive().GetMethodPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearEveryPreferenceWhenTheUpdateIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        GivenSuccessfulReplace(userId);
        GivenStoredPreferences(userId, [], []);

        // Act
        var result = await Sut.UpdatePreferencesAsync(
            userId,
            new UpdateFishingPreferencesDto([]),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Methods.Should().BeEmpty();
        await MockFishingPreferenceRepository.Received(1).ReplacePreferencesAsync(
            userId,
            Arg.Is<IReadOnlyList<UserFishingMethodPreference>>(methods => methods.Count == 0),
            Arg.Is<IReadOnlyList<UserFishingSpeciesPreference>>(species => species.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistPreferencesOwnedByTheAuthenticatedUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        GivenSuccessfulReplace(userId);
        GivenStoredPreferences(
            userId,
            [new UserFishingMethodPreference { UserId = userId, FishingMethodId = FlyMethodId, IsDefault = true }],
            [
                new UserFishingSpeciesPreference
                {
                    UserId = userId,
                    FishingMethodId = FlyMethodId,
                    SpeciesId = BrownTroutSpeciesId,
                    IsDefault = true
                }
            ]);

        // Act
        var result = await Sut.UpdatePreferencesAsync(userId, ValidUpdate(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Methods.Should().HaveCount(1);
        result.Value.Methods[0].FishingMethodId.Should().Be(FlyMethodId);
        result.Value.Methods[0].IsDefault.Should().BeTrue();
        result.Value.Methods[0].Species[0].SpeciesId.Should().Be(BrownTroutSpeciesId);
        await MockFishingPreferenceRepository.Received(1).ReplacePreferencesAsync(
            userId,
            Arg.Is<IReadOnlyList<UserFishingMethodPreference>>(methods =>
                methods.Count == 1
                && methods[0].UserId == userId
                && methods[0].FishingMethodId == FlyMethodId
                && methods[0].IsDefault),
            Arg.Is<IReadOnlyList<UserFishingSpeciesPreference>>(species =>
                species.Count == 1
                && species[0].UserId == userId
                && species[0].FishingMethodId == FlyMethodId
                && species[0].SpeciesId == BrownTroutSpeciesId
                && species[0].IsDefault),
            Arg.Any<CancellationToken>());
        await MockFishingPreferenceRepository.Received(1).GetMethodPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    private static UpdateFishingPreferencesDto ValidUpdate()
    {
        return new UpdateFishingPreferencesDto(
        [
            new UpdateFishingMethodPreferenceDto(
                FlyMethodId,
                true,
                [new UpdateFishingSpeciesPreferenceDto(BrownTroutSpeciesId, true)])
        ]);
    }

    private void GivenCatalogue()
    {
        MockFishingCatalogueRepository
            .GetAllMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok(CatalogueMethods()));
        MockFishingCatalogueRepository
            .GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok(CatalogueSpecies()));
    }

    private void GivenSuccessfulReplace(Guid userId)
    {
        MockFishingPreferenceRepository
            .ReplacePreferencesAsync(
                userId,
                Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
                Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }

    private void GivenStoredPreferences(
        Guid userId,
        IReadOnlyList<UserFishingMethodPreference> methods,
        IReadOnlyList<UserFishingSpeciesPreference> species)
    {
        MockFishingPreferenceRepository
            .GetMethodPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(methods));
        MockFishingPreferenceRepository
            .GetSpeciesPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(species));
    }
}
