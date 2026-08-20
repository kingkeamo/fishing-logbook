using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.FishingPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetMethodPreferences : BaseFishingPreferenceRepositoryTest
{
    public WhenTestingGetMethodPreferences(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNothingForAnUnknownUser()
    {
        // Arrange
        // Act
        var result = await Sut.GetMethodPreferencesAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnOnlyThePreferencesOwnedByTheUser()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var flyId = await MethodIdAsync("Fly");
        var spinningId = await MethodIdAsync("Spinning");
        await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, flyId, true)],
            [],
            CancellationToken.None);
        await Sut.ReplacePreferencesAsync(
            otherUserId,
            [MethodPreference(otherUserId, spinningId, true)],
            [],
            CancellationToken.None);

        // Act
        var result = await Sut.GetMethodPreferencesAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(userId);
        result.Value[0].FishingMethodId.Should().Be(flyId);
        result.Value[0].IsDefault.Should().BeTrue();
        result.Value[0].CreatedOn.Should().NotBe(default);
    }
}
