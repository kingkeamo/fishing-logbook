using AwesomeAssertions;
using FishingLogBook.Domain.Catalogue;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Services.FishingPreferenceServiceTests;

public class WhenTestingGetPreferences : BaseFishingPreferenceServiceTest
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
        var result = await Sut.GetPreferencesAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load fishing method catalogue.");
        await MockFishingPreferenceRepository.DidNotReceive().GetMethodPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenMethodPreferencesCannotBeLoaded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        MockFishingPreferenceRepository
            .GetMethodPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<UserFishingMethodPreference>>(
                "Failed to load fishing method preferences."));

        // Act
        var result = await Sut.GetPreferencesAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load fishing method preferences.");
        await MockFishingPreferenceRepository.Received(1).GetMethodPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
        await MockFishingPreferenceRepository.DidNotReceive().GetSpeciesPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenSpeciesPreferencesCannotBeLoaded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        MockFishingPreferenceRepository
            .GetMethodPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<UserFishingMethodPreference>>([]));
        MockFishingPreferenceRepository
            .GetSpeciesPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<UserFishingSpeciesPreference>>(
                "Failed to load fishing species preferences."));

        // Act
        var result = await Sut.GetPreferencesAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load fishing species preferences.");
        await MockFishingPreferenceRepository.Received(1).GetSpeciesPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNoMethodsWhenTheUserHasNoPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        GivenPreferences(userId, [], []);

        // Act
        var result = await Sut.GetPreferencesAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Methods.Should().BeEmpty();
        await MockFishingPreferenceRepository.Received(1).GetMethodPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
        await MockFishingPreferenceRepository.Received(1).GetSpeciesPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDropPreferencesWhoseCatalogueEntryNoLongerExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        GivenPreferences(
            userId,
            [
                new UserFishingMethodPreference { UserId = userId, FishingMethodId = FlyMethodId },
                new UserFishingMethodPreference { UserId = userId, FishingMethodId = UnknownId }
            ],
            [
                new UserFishingSpeciesPreference
                {
                    UserId = userId,
                    FishingMethodId = FlyMethodId,
                    SpeciesId = UnknownId
                }
            ]);

        // Act
        var result = await Sut.GetPreferencesAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Methods.Should().HaveCount(1);
        result.Value.Methods[0].FishingMethodId.Should().Be(FlyMethodId);
        result.Value.Methods[0].Species.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOrderTheDefaultMethodFirstThenByName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        GivenCatalogue();
        GivenPreferences(
            userId,
            [
                new UserFishingMethodPreference { UserId = userId, FishingMethodId = FlyMethodId },
                new UserFishingMethodPreference
                {
                    UserId = userId,
                    FishingMethodId = SpinningMethodId,
                    IsDefault = true
                }
            ],
            [
                new UserFishingSpeciesPreference
                {
                    UserId = userId,
                    FishingMethodId = SpinningMethodId,
                    SpeciesId = BrownTroutSpeciesId
                },
                new UserFishingSpeciesPreference
                {
                    UserId = userId,
                    FishingMethodId = SpinningMethodId,
                    SpeciesId = PikeSpeciesId,
                    IsDefault = true
                }
            ]);

        // Act
        var result = await Sut.GetPreferencesAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Methods.Should().HaveCount(2);
        result.Value.Methods[0].Name.Should().Be("Spinning");
        result.Value.Methods[0].IsDefault.Should().BeTrue();
        result.Value.Methods[0].Species[0].Name.Should().Be("Pike");
        result.Value.Methods[0].Species[0].IsDefault.Should().BeTrue();
        result.Value.Methods[0].Species[1].Name.Should().Be("Brown Trout");
        result.Value.Methods[1].Name.Should().Be("Fly");
        result.Value.Methods[1].IsDefault.Should().BeFalse();
        result.Value.Methods[1].Species.Should().BeEmpty();
        await MockFishingCatalogueRepository.Received(1).GetAllMethodsAsync(Arg.Any<CancellationToken>());
        await MockFishingCatalogueRepository.Received(1).GetAllSpeciesAsync(Arg.Any<CancellationToken>());
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

    private void GivenPreferences(
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
