using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Stores.FileOfflineAccountStoreTests;

public sealed class WhenTestingSave : BaseFileOfflineAccountStoreTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyAccountOwner()
    {
        // Arrange
        var invalid = Account() with { UserId = Guid.Empty };
        var action = () => Sut.SaveAsync(invalid, CancellationToken.None);

        // Act

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
        MockFileSystem.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAtomicallyPersistTheAccountSnapshotWithoutTokens()
    {
        // Arrange
        var account = Account();

        // Act
        await Sut.SaveAsync(account, CancellationToken.None);

        // Assert
        var created = CreatedFiles.Should().ContainSingle().Subject;
        var persisted = JsonSerializer.Deserialize<OfflineAccountModel>(
            created.Stream.ToArray(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        persisted.Should().BeEquivalentTo(account);
        MockFileSystem.Received(1).MoveFile(created.Path, PathFor(UserId), true);
        var json = System.Text.Encoding.UTF8.GetString(created.Stream.ToArray());
        json.Should().NotContainEquivalentOf("accessToken");
        json.Should().NotContainEquivalentOf("refreshToken");
    }
}
