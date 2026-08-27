using AwesomeAssertions;
using FishingLogBook.Application.FishingLocations.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingLocations.Queries.GetFishingLocationPreferencesQueryTests;

public class WhenTestingHandle : BaseGetFishingLocationPreferencesQueryTest
{
    [Fact]
    public async Task ItShouldReturnTheErrorMessageWhenTheServiceFails()
    {
        // Arrange
        MockFishingLocationPreferenceService
            .GetPreferencesAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<FishingLocationPreferencesDto>("Failed to load fishing locations."));

        // Act
        var response = await Sut.Handle(
            new GetFishingLocationPreferencesQuery { UserId = OwnerUserId },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to load fishing locations.");
        response.Locations.Should().BeNull();
        await MockFishingLocationPreferenceService.Received(1).GetPreferencesAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNoLocationsWhenNoneAreSaved()
    {
        // Arrange
        MockFishingLocationPreferenceService
            .GetPreferencesAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FishingLocationPreferencesDto([])));

        // Act
        var response = await Sut.Handle(
            new GetFishingLocationPreferencesQuery { UserId = OwnerUserId },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Locations!.Locations.Should().BeEmpty();
        await MockFishingLocationPreferenceService.Received(1).GetPreferencesAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheLocationsForTheRequestedAnglerOnly()
    {
        // Arrange
        MockFishingLocationPreferenceService
            .GetPreferencesAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FishingLocationPreferencesDto(
                [new FishingLocationPreferenceDto(CorribId, "Lough Corrib", true)])));

        // Act
        var response = await Sut.Handle(
            new GetFishingLocationPreferencesQuery { UserId = OwnerUserId },
            CancellationToken.None);

        // Assert
        response.Locations!.Locations.Single().Name.Should().Be("Lough Corrib");
        response.Locations.Locations.Single().IsDefault.Should().BeTrue();
        await MockFishingLocationPreferenceService.Received(1).GetPreferencesAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
        await MockFishingLocationPreferenceService.DidNotReceive().GetPreferencesAsync(
            Arg.Is<Guid>(userId => userId != OwnerUserId),
            Arg.Any<CancellationToken>());
    }
}
