using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.ProfileRepositoryTests;

public class WhenTestingCompleteOnboarding : BaseProfileRepositoryTest
{
    public WhenTestingCompleteOnboarding(PostgresFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldCreateAndCompleteAMissingProfile()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.CompleteOnboardingAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.OnboardingCompletedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldRemainCompletedWhenReplayed()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var first = await Sut.CompleteOnboardingAsync(userId, CancellationToken.None);

        // Act
        var second = await Sut.CompleteOnboardingAsync(userId, CancellationToken.None);

        // Assert
        second.IsSuccess.Should().BeTrue();
        second.Value.OnboardingCompletedOn.Should().Be(first.Value.OnboardingCompletedOn);
    }
}
