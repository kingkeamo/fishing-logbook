using AwesomeAssertions;
using FishingLogBook.Application.FishingLocations.Errors;
using FishingLogBook.Domain.FishingLocations;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingLocations.Services.FishingLocationPreferenceServiceTests;

public class WhenTestingUpdatePreferences : BaseFishingLocationPreferenceServiceTest
{
    [Fact]
    public async Task ItShouldReturnTheRepositoryFailureWithoutReReading()
    {
        // Arrange
        MockFishingLocationPreferenceRepository
            .ReplaceAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail(new DuplicateFishingLocationError(
                "A fishing location with that name is already saved.")));

        // Act
        var result = await Sut.UpdatePreferencesAsync(
            OwnerUserId,
            new UpdateFishingLocationPreferencesDto(
                [new UpdateFishingLocationPreferenceDto(Guid.Empty, "Lough Corrib", true)]),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<DuplicateFishingLocationError>();
        await MockFishingLocationPreferenceRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceWithNoLocationsWhenTheListIsEmpty()
    {
        // Arrange
        GivenStored();

        // Act
        var result = await Sut.UpdatePreferencesAsync(
            OwnerUserId,
            new UpdateFishingLocationPreferencesDto([]),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Locations.Should().BeEmpty();
        await MockFishingLocationPreferenceRepository.Received(1).ReplaceAsync(
            OwnerUserId,
            Arg.Is<IReadOnlyList<UserFishingLocationPreference>>(locations => locations.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTrimNamesAndOwnEveryLocationToTheRequestingAngler()
    {
        // Arrange
        GivenStored(Stored(CorribId, "Lough Corrib", true));

        // Act
        await Sut.UpdatePreferencesAsync(
            OwnerUserId,
            new UpdateFishingLocationPreferencesDto(
                [new UpdateFishingLocationPreferenceDto(CorribId, "  Lough Corrib  ", true)]),
            CancellationToken.None);

        // Assert
        await MockFishingLocationPreferenceRepository.Received(1).ReplaceAsync(
            OwnerUserId,
            Arg.Is<IReadOnlyList<UserFishingLocationPreference>>(locations =>
                locations.Count == 1 &&
                locations[0].Name == "Lough Corrib" &&
                locations[0].UserId == OwnerUserId &&
                locations[0].Id == CorribId &&
                locations[0].IsDefault),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAssignAnIdentityToANewlyAddedLocation()
    {
        // Arrange
        GivenStored(Stored(CorribId, "Lough Corrib", true));

        // Act
        await Sut.UpdatePreferencesAsync(
            OwnerUserId,
            new UpdateFishingLocationPreferencesDto(
            [
                new UpdateFishingLocationPreferenceDto(CorribId, "Lough Corrib", true),
                new UpdateFishingLocationPreferenceDto(Guid.Empty, "River Moy", false)
            ]),
            CancellationToken.None);

        // Assert
        await MockFishingLocationPreferenceRepository.Received(1).ReplaceAsync(
            OwnerUserId,
            Arg.Is<IReadOnlyList<UserFishingLocationPreference>>(locations =>
                locations.Count == 2 &&
                locations[1].Name == "River Moy" &&
                locations[1].Id != Guid.Empty &&
                locations[1].Id != CorribId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveNoDefaultWhenTheAnglerRemovesTheDefaultLocation()
    {
        // Arrange
        GivenStored(Stored(MoyId, "River Moy"));

        // Act
        var result = await Sut.UpdatePreferencesAsync(
            OwnerUserId,
            new UpdateFishingLocationPreferencesDto(
                [new UpdateFishingLocationPreferenceDto(MoyId, "River Moy", false)]),
            CancellationToken.None);

        // Assert
        result.Value.Locations.Should().OnlyContain(location => !location.IsDefault);
        await MockFishingLocationPreferenceRepository.Received(1).ReplaceAsync(
            OwnerUserId,
            Arg.Is<IReadOnlyList<UserFishingLocationPreference>>(locations =>
                locations.Count == 1 && !locations[0].IsDefault),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheStoredLocationsAfterSaving()
    {
        // Arrange
        GivenStored(Stored(MoyId, "River Moy", true), Stored(CorribId, "Lough Corrib"));

        // Act
        var result = await Sut.UpdatePreferencesAsync(
            OwnerUserId,
            new UpdateFishingLocationPreferencesDto(
            [
                new UpdateFishingLocationPreferenceDto(CorribId, "Lough Corrib", false),
                new UpdateFishingLocationPreferenceDto(MoyId, "River Moy", true)
            ]),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Locations.Select(location => location.Name)
            .Should().Equal("River Moy", "Lough Corrib");
        await MockFishingLocationPreferenceRepository.Received(1).GetByUserIdAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }
}
