using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingGetPhotographState : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldReturnNullWhenThereIsNoState()
    {
        // Arrange
        await Harness.Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var state = await Sut.GetPhotographStateAsync(OwnerUserId, Guid.NewGuid(), CancellationToken.None);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnNullForAnotherUsersState()
    {
        // Arrange
        var photographId = Guid.NewGuid();
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, Guid.NewGuid(), photographId, PhotographSyncStatusEnum.Uploaded, "k"),
            Now,
            CancellationToken.None);

        // Act
        var state = await Sut.GetPhotographStateAsync(OtherUserId, photographId, CancellationToken.None);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnTheStoredState()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, catchId, photographId, PhotographSyncStatusEnum.Uploaded, "k"),
            Now,
            CancellationToken.None);

        // Act
        var state = await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None);

        // Assert
        state!.PhotographId.Should().Be(photographId);
        state.CatchId.Should().Be(catchId);
        state.UserId.Should().Be(OwnerUserId);
        state.Status.Should().Be(PhotographSyncStatusEnum.Uploaded);
        state.ObjectKey.Should().Be("k");
    }
}
