using AwesomeAssertions;
using FishingLogBook.Application.FishingPreferences.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Queries.GetFishingPreferencesQueryTests;

public class WhenTestingHandle : BaseGetFishingPreferencesQueryTest
{
    [Fact]
    public async Task ItShouldReturnTheErrorMessageWhenTheServiceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockFishingPreferenceService
            .GetPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<FishingPreferencesDto>("Failed to load fishing method preferences."));

        // Act
        var response = await Sut.Handle(
            new GetFishingPreferencesQuery { UserId = userId },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to load fishing method preferences.");
        response.Preferences.Should().BeNull();
        await MockFishingPreferenceService.Received(1).GetPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnAnEmptySelectionWhenTheUserHasNoPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockFishingPreferenceService
            .GetPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FishingPreferencesDto([])));

        // Act
        var response = await Sut.Handle(
            new GetFishingPreferencesQuery { UserId = userId },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Preferences!.Methods.Should().BeEmpty();
        await MockFishingPreferenceService.Received(1).GetPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnThePreferencesForTheRequestedUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var preferences = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(FlyMethodId, "Fly", "Fly", true, [])
        ]);
        MockFishingPreferenceService
            .GetPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(preferences));

        // Act
        var response = await Sut.Handle(
            new GetFishingPreferencesQuery { UserId = userId },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Preferences.Should().BeSameAs(preferences);
        await MockFishingPreferenceService.Received(1).GetPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
        await MockFishingPreferenceService.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }
}
