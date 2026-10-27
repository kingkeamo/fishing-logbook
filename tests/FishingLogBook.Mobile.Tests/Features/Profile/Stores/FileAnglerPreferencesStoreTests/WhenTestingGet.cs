using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Stores.FileAnglerPreferencesStoreTests;

public class WhenTestingGet : BaseFileAnglerPreferencesStoreTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyAccount()
    {
        // Arrange
        var action = () => Sut.GetAsync(Guid.Empty, CancellationToken.None);
        // Act
        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ItShouldSurfaceACorruptCacheFileToTheCaller()
    {
        // Arrange
        System.IO.Directory.CreateDirectory(Directory);
        await File.WriteAllTextAsync(PathFor(FirstUserId), "{\"catalogue\":");
        var action = () => Sut.GetAsync(FirstUserId, CancellationToken.None);
        // Act
        // Assert
        await action.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheAccountHasNoCache()
    {
        // Arrange
        // Act
        var result = await Sut.GetAsync(FirstUserId, CancellationToken.None);
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNeverReturnAnotherAccountsPreferences()
    {
        // Arrange
        await Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        // Act
        var result = await Sut.GetAsync(SecondUserId, CancellationToken.None);
        // Assert
        result.Should().BeNull();
        File.Exists(PathFor(SecondUserId)).Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReturnTheSavedPreferencesForTheSameAccount()
    {
        // Arrange
        var saved = TestAnglerPreferences.Create();
        await Sut.SaveAsync(FirstUserId, saved, CancellationToken.None);
        // Act
        var result = await Sut.GetAsync(FirstUserId, CancellationToken.None);
        // Assert
        result.Should().BeEquivalentTo(saved);
    }
}
