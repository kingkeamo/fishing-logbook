using AwesomeAssertions;
using FishingLogBook.Domain.FishingLocations;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingLocations.Services.FishingLocationPreferenceServiceTests;

public class WhenTestingGetPreferences : BaseFishingLocationPreferenceServiceTest
{
    [Fact]
    public async Task ItShouldReturnTheRepositoryFailure()
    {
        // Arrange
        MockFishingLocationPreferenceRepository
            .GetByUserIdAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<UserFishingLocationPreference>>(
                "Failed to load fishing locations."));

        // Act
        var result = await Sut.GetPreferencesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load fishing locations.");
        await MockFishingLocationPreferenceRepository.Received(1).GetByUserIdAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNoLocationsWhenNoneAreSaved()
    {
        // Arrange
        GivenStored();

        // Act
        var result = await Sut.GetPreferencesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Locations.Should().BeEmpty();
        await MockFishingLocationPreferenceRepository.Received(1).GetByUserIdAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheDefaultFirstThenNamesAlphabetically()
    {
        // Arrange
        GivenStored(
            Stored(MoyId, "River Moy"),
            Stored(CorribId, "Lough Corrib"),
            Stored(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"), "Lough Mask", true));

        // Act
        var result = await Sut.GetPreferencesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        result.Value.Locations.Select(location => location.Name)
            .Should().Equal("Lough Mask", "Lough Corrib", "River Moy");
        result.Value.Locations.Count(location => location.IsDefault).Should().Be(1);
        result.Value.Locations[0].Id.Should().Be(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"));
    }
}
