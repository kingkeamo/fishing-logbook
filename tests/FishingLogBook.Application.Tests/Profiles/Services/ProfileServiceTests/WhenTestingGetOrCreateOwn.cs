using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingGetOrCreateOwn : BaseProfileServiceTest
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
        var result = await Sut.GetOrCreateOwnAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenCreationFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile>("Failed to load angler profile."));

        // Act
        var result = await Sut.GetOrCreateOwnAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile => profile.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCreateADefaultProfileWhenNoneExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));

        // Act
        var result = await Sut.GetOrCreateOwnAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Location.Should().BeNull();
        result.Value.ShowDisplayName.Should().BeTrue();
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.UserId == userId
                && profile.LocationVisibility == null
                && profile.Latitude == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheExistingProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existing = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .Build();
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(existing));

        // Act
        var result = await Sut.GetOrCreateOwnAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Eamonn");
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }
}
