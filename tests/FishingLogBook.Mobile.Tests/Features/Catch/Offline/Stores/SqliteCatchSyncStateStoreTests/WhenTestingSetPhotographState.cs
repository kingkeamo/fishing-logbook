using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingSetPhotographState : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldNotOverwriteAnotherUsersPhotographState()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await SetAsync(State(OwnerUserId, catchId, photographId, PhotographSyncStatusEnum.Uploaded, "owner/key"));

        // Act
        await SetAsync(State(OtherUserId, catchId, photographId, PhotographSyncStatusEnum.Recorded, "other/key"));

        // Assert
        var state = await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None);
        state!.Status.Should().Be(PhotographSyncStatusEnum.Uploaded);
        state.ObjectKey.Should().Be("owner/key");
        (await Sut.GetPhotographStateAsync(OtherUserId, photographId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldInsertANewStateWithItsObjectKey()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();

        // Act
        await SetAsync(State(OwnerUserId, catchId, photographId, PhotographSyncStatusEnum.Uploaded, "owner/key"));

        // Assert
        var state = await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None);
        state!.CatchId.Should().Be(catchId);
        state.Status.Should().Be(PhotographSyncStatusEnum.Uploaded);
        state.ObjectKey.Should().Be("owner/key");
    }

    [Fact]
    public async Task ItShouldUpdateTheStatusAndObjectKeyOfAnExistingState()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await SetAsync(State(OwnerUserId, catchId, photographId, PhotographSyncStatusEnum.Uploaded, "owner/key"));

        // Act
        await SetAsync(State(OwnerUserId, catchId, photographId, PhotographSyncStatusEnum.Recorded, "owner/key2"));

        // Assert
        var states = await Sut.ListPhotographStatesAsync(OwnerUserId, catchId, CancellationToken.None);
        states.Should().ContainSingle();
        states[0].Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        states[0].ObjectKey.Should().Be("owner/key2");
    }

    [Fact]
    public async Task ItShouldSurviveResavingTheCatchAndReplacingItsPhotographs()
    {
        // Arrange
        var args = await SaveCatchAsync();
        var photographId = args.Photographs[0].Id;
        await SetAsync(State(OwnerUserId, args.Id, photographId, PhotographSyncStatusEnum.Recorded, "owner/key"));

        // Act
        var resave = await OwnerCatchStore.SaveAsync(args, CancellationToken.None);

        // Assert
        resave.IsSuccess.Should().BeTrue();
        var state = await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None);
        state!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        state.ObjectKey.Should().Be("owner/key");
    }

    private Task SetAsync(PhotographSyncStateModel state)
    {
        return Harness.Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.SetPhotographStateAsync(connection, transaction, state, Now, ct),
            CancellationToken.None);
    }
}
