using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.FishingLocationPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetByUserId : BaseFishingLocationPreferenceRepositoryTest
{
    public WhenTestingGetByUserId(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNoLocationsForAnUnknownUser()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnNoLocationsWhenTheAnglerHasSavedNone()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnOnlyTheOwnersLocations()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        await Sut.ReplaceAsync(
            ownerUserId,
            [Location(ownerUserId, "Lough Corrib", true), Location(ownerUserId, "River Moy")],
            CancellationToken.None);
        await Sut.ReplaceAsync(
            otherUserId,
            [Location(otherUserId, "Lough Mask", true)],
            CancellationToken.None);

        // Act
        var result = await Sut.GetByUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value.Select(location => location.Name).Should().Equal("Lough Corrib", "River Moy");
        result.Value.Should().OnlyContain(location => location.UserId == ownerUserId);
    }

    [Fact]
    public async Task ItShouldReturnTheDefaultFirstThenNamesInOrder()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await Sut.ReplaceAsync(
            userId,
            [
                Location(userId, "River Moy"),
                Location(userId, "lough corrib"),
                Location(userId, "Lough Mask", true)
            ],
            CancellationToken.None);

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.Value.Select(location => location.Name).Should().Equal("Lough Mask", "lough corrib", "River Moy");
        result.Value.Count(location => location.IsDefault).Should().Be(1);
    }
}
