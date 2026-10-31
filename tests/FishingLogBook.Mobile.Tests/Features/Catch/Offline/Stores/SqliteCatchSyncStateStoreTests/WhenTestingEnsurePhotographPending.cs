using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingEnsurePhotographPending : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldNotDowngradeAnExistingRecordedPhotograph()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await Harness.Database.EnsureInitializedAsync(CancellationToken.None);
        await Harness.Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.SetPhotographStateAsync(
                connection,
                transaction,
                State(OwnerUserId, catchId, photographId, PhotographSyncStatusEnum.Recorded, "key/1"),
                Now,
                ct),
            CancellationToken.None);

        // Act
        await EnsureAsync(OwnerUserId, catchId, photographId);

        // Assert
        var state = await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None);
        state!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        state.ObjectKey.Should().Be("key/1");
    }

    [Fact]
    public async Task ItShouldRollBackWhenTheCallersTransactionFails()
    {
        // Arrange
        var photographId = Guid.NewGuid();
        await Harness.Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var act = async () => await Harness.Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await Sut.EnsurePhotographPendingAsync(
                    connection, transaction, OwnerUserId, Guid.NewGuid(), photographId, Now, ct);
                throw new InvalidOperationException("simulated failure");
            },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Sut.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldCreateAPendingRowWithoutAnObjectKey()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await Harness.Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        await EnsureAsync(OwnerUserId, catchId, photographId);
        await EnsureAsync(OwnerUserId, catchId, photographId);

        // Assert
        var states = await Sut.ListPhotographStatesAsync(OwnerUserId, catchId, CancellationToken.None);
        states.Should().ContainSingle();
        states[0].PhotographId.Should().Be(photographId);
        states[0].CatchId.Should().Be(catchId);
        states[0].UserId.Should().Be(OwnerUserId);
        states[0].Status.Should().Be(PhotographSyncStatusEnum.Pending);
        states[0].ObjectKey.Should().BeNull();
    }

    private Task EnsureAsync(Guid userId, Guid catchId, Guid photographId)
    {
        return Harness.Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.EnsurePhotographPendingAsync(
                connection, transaction, userId, catchId, photographId, Now, ct),
            CancellationToken.None);
    }
}
