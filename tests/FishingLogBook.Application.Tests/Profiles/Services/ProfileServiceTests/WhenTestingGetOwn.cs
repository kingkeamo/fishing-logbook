using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingGetOwn : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheLookupFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile?>("Failed to load angler profile."));

        // Act
        var result = await Sut.GetOwnAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnAnInMemoryDefaultWithoutPersistingWhenNoProfileExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));

        // Act
        var result = await Sut.GetOwnAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.DisplayName.Should().BeNull();
        result.Value.HomeRegion.Should().BeNull();
        result.Value.PhotographUrl.Should().BeNull();
        result.Value.PreferredFishingTypes.Should().BeEmpty();
        result.Value.PreferredSpecies.Should().BeEmpty();
        result.Value.ShowDisplayName.Should().BeTrue();
        result.Value.ShowPhotograph.Should().BeFalse();
        result.Value.ShowHomeRegion.Should().BeFalse();
        result.Value.ShowPreferredFishingTypes.Should().BeFalse();
        result.Value.ShowPreferredSpecies.Should().BeFalse();
        typeof(ProfileDto).GetProperty("Latitude").Should().BeNull();
        typeof(ProfileDto).GetProperty("Longitude").Should().BeNull();
        typeof(ProfileDto).GetProperty("Location").Should().BeNull();
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheExistingProfileWithoutWriting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existing = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .WithHomeRegion("Westmeath")
            .WithFishingTypes("Coarse")
            .WithSpecies("Pike")
            .Build();
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(existing));

        // Act
        var result = await Sut.GetOwnAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredFishingTypes.Should().Equal("Coarse");
        result.Value.PreferredSpecies.Should().Equal("Pike");
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }
}
