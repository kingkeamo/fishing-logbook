using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

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
        MockFileSystem.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldSurfaceACorruptCacheFileToTheCaller()
    {
        // Arrange
        GivenStoredJson(FirstUserId, "{\"catalogue\":");
        var action = () => Sut.GetAsync(FirstUserId, CancellationToken.None);
        // Act
        // Assert
        await action.Should().ThrowAsync<JsonException>();
        MockFileSystem.Received(1).OpenRead(PathFor(FirstUserId));
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheAccountHasNoCache()
    {
        // Arrange
        // Act
        var result = await Sut.GetAsync(FirstUserId, CancellationToken.None);
        // Assert
        result.Should().BeNull();
        MockFileSystem.Received(1).FileExists(PathFor(FirstUserId));
        MockFileSystem.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldNeverReturnAnotherAccountsPreferences()
    {
        // Arrange
        GivenStoredJson(FirstUserId, JsonSerializer.Serialize(TestAnglerPreferences.Create(), SerializerOptions));
        // Act
        var result = await Sut.GetAsync(SecondUserId, CancellationToken.None);
        // Assert
        result.Should().BeNull();
        MockFileSystem.Received(1).FileExists(PathFor(SecondUserId));
        MockFileSystem.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldReturnTheSavedPreferencesForTheSameAccount()
    {
        // Arrange
        var saved = TestAnglerPreferences.Create();
        GivenStoredJson(FirstUserId, JsonSerializer.Serialize(saved, SerializerOptions));
        // Act
        var result = await Sut.GetAsync(FirstUserId, CancellationToken.None);
        // Assert
        result.Should().BeEquivalentTo(saved);
        MockFileSystem.Received(1).OpenRead(PathFor(FirstUserId));
    }
}
