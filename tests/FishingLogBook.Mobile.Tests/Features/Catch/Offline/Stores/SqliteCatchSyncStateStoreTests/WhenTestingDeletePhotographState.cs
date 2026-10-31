using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingDeletePhotographState : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldDoNothingWhenTheStateDoesNotExist()
    {
        // Arrange
        await Harness.Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var act = async () => await Sut.DeletePhotographStateAsync(OwnerUserId, Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItShouldNotDeleteAnotherUsersState()
    {
        // Arrange
        var photographId = Guid.NewGuid();
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, Guid.NewGuid(), photographId, PhotographSyncStatusEnum.Uploaded, "k"),
            Now,
            CancellationToken.None);

        // Act
        await Sut.DeletePhotographStateAsync(OtherUserId, photographId, CancellationToken.None);

        // Assert
        (await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldDeleteOnlyTheRequestedState()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        var kept = Guid.NewGuid();
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, catchId, deleted, PhotographSyncStatusEnum.Uploaded, "a"), Now, CancellationToken.None);
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, catchId, kept, PhotographSyncStatusEnum.Uploaded, "b"), Now, CancellationToken.None);

        // Act
        await Sut.DeletePhotographStateAsync(OwnerUserId, deleted, CancellationToken.None);

        // Assert
        (await Sut.GetPhotographStateAsync(OwnerUserId, deleted, CancellationToken.None)).Should().BeNull();
        (await Sut.GetPhotographStateAsync(OwnerUserId, kept, CancellationToken.None)).Should().NotBeNull();
    }
}
