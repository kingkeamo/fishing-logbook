using AwesomeAssertions;
using FishingLogBook.Application.FishingLocations.Commands;
using FishingLogBook.Application.FishingLocations.Errors;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingLocations.Commands.UpdateFishingLocationPreferencesCommandTests;

public class WhenTestingHandle : BaseUpdateFishingLocationPreferencesCommandTest
{
    [Fact]
    public async Task ItShouldSurfaceADuplicateLocationError()
    {
        // Arrange
        var locations = new UpdateFishingLocationPreferencesDto(
            [new UpdateFishingLocationPreferenceDto(Guid.Empty, "Lough Corrib", true)]);
        MockFishingLocationPreferenceService
            .UpdatePreferencesAsync(OwnerUserId, locations, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<FishingLocationPreferencesDto>(new DuplicateFishingLocationError(
                "A fishing location with that name is already saved.")));

        // Act
        var response = await Sut.Handle(
            new UpdateFishingLocationPreferencesCommand
            {
                UserId = OwnerUserId,
                Locations = locations
            },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<DuplicateFishingLocationError>();
        response.Locations.Should().BeNull();
        await MockFishingLocationPreferenceService.Received(1).UpdatePreferencesAsync(
            OwnerUserId,
            Arg.Is<UpdateFishingLocationPreferencesDto>(dto =>
                dto.Locations.Count == 1 && dto.Locations[0].Name == "Lough Corrib"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveAnEmptyListForTheAuthenticatedAngler()
    {
        // Arrange
        var locations = new UpdateFishingLocationPreferencesDto([]);
        MockFishingLocationPreferenceService
            .UpdatePreferencesAsync(OwnerUserId, locations, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FishingLocationPreferencesDto([])));

        // Act
        var response = await Sut.Handle(
            new UpdateFishingLocationPreferencesCommand
            {
                UserId = OwnerUserId,
                Locations = locations
            },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Locations!.Locations.Should().BeEmpty();
        await MockFishingLocationPreferenceService.Received(1).UpdatePreferencesAsync(
            OwnerUserId,
            Arg.Is<UpdateFishingLocationPreferencesDto>(dto => dto.Locations.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheLocationsForTheAuthenticatedAngler()
    {
        // Arrange
        var locations = new UpdateFishingLocationPreferencesDto(
        [
            new UpdateFishingLocationPreferenceDto(CorribId, "Lough Corrib", true),
            new UpdateFishingLocationPreferenceDto(Guid.Empty, "River Moy", false)
        ]);
        MockFishingLocationPreferenceService
            .UpdatePreferencesAsync(OwnerUserId, locations, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FishingLocationPreferencesDto(
            [
                new FishingLocationPreferenceDto(CorribId, "Lough Corrib", true),
                new FishingLocationPreferenceDto(Guid.NewGuid(), "River Moy", false)
            ])));

        // Act
        var response = await Sut.Handle(
            new UpdateFishingLocationPreferencesCommand
            {
                UserId = OwnerUserId,
                Locations = locations
            },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Locations!.Locations.Select(location => location.Name)
            .Should().Equal("Lough Corrib", "River Moy");
        await MockFishingLocationPreferenceService.Received(1).UpdatePreferencesAsync(
            OwnerUserId,
            Arg.Is<UpdateFishingLocationPreferencesDto>(dto =>
                dto.Locations.Count == 2 &&
                dto.Locations[0].IsDefault &&
                !dto.Locations[1].IsDefault),
            Arg.Any<CancellationToken>());
        await MockFishingLocationPreferenceService.DidNotReceive().UpdatePreferencesAsync(
            Arg.Is<Guid>(userId => userId != OwnerUserId),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }
}
