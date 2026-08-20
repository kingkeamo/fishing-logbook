using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingCompleteOnboarding : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheProfileCannotBeCompleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository.CompleteOnboardingAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile>("failed"));

        // Act
        var result = await Sut.CompleteOnboardingAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockProfileRepository.Received(1).CompleteOnboardingAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnACompletedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository.CompleteOnboardingAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new Profile { UserId = userId, OnboardingCompletedOn = DateTimeOffset.UtcNow }));

        // Act
        var result = await Sut.CompleteOnboardingAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.OnboardingCompleted.Should().BeTrue();
        await MockProfileRepository.Received(1).CompleteOnboardingAsync(userId, Arg.Any<CancellationToken>());
    }
}
