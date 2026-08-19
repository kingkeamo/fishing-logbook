using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Integration.FishingPreferences.FishingPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetSpeciesPreferences : BaseFishingPreferenceRepositoryTest
{
    public WhenTestingGetSpeciesPreferences(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNothingForAnUnknownUser()
    {
        // Arrange
        // Act
        var result = await Sut.GetSpeciesPreferencesAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnEverySpeciesGroupedByItsFishingMethod()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var flyId = await MethodIdAsync("Fly");
        var spinningId = await MethodIdAsync("Spinning");
        var brownTroutId = await SpeciesIdAsync("BrownTrout");
        var pikeId = await SpeciesIdAsync("Pike");
        await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, flyId, true), MethodPreference(userId, spinningId)],
            [
                SpeciesPreference(userId, flyId, brownTroutId, true),
                SpeciesPreference(userId, spinningId, pikeId, true)
            ],
            CancellationToken.None);

        // Act
        var result = await Sut.GetSpeciesPreferencesAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(species => species.UserId == userId);
        result.Value.Should().ContainSingle(species =>
            species.FishingMethodId == flyId && species.SpeciesId == brownTroutId && species.IsDefault);
        result.Value.Should().ContainSingle(species =>
            species.FishingMethodId == spinningId && species.SpeciesId == pikeId && species.IsDefault);
    }
}
