using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingSetPhotographUploaded : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldNotOverwriteAnotherUsersPhotographState()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, catchId, photographId, PhotographSyncStatusEnum.Pending),
            Now,
            CancellationToken.None);

        // Act
        await Sut.SetPhotographUploadedAsync(
            State(OtherUserId, catchId, photographId, PhotographSyncStatusEnum.Uploaded, "other/key"),
            Now,
            CancellationToken.None);

        // Assert
        var state = await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None);
        state!.Status.Should().Be(PhotographSyncStatusEnum.Pending);
        state.ObjectKey.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPersistTheUploadedStatusAndObjectKeyInItsOwnTransaction()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();

        // Act
        await Sut.SetPhotographUploadedAsync(
            State(OwnerUserId, catchId, photographId, PhotographSyncStatusEnum.Uploaded, "owner/key"),
            Now,
            CancellationToken.None);

        // Assert
        var state = await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None);
        state!.Status.Should().Be(PhotographSyncStatusEnum.Uploaded);
        state.ObjectKey.Should().Be("owner/key");
        state.CatchId.Should().Be(catchId);
    }
}
