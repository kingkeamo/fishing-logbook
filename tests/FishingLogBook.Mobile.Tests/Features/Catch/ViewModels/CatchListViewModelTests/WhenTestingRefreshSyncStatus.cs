using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingRefreshSyncStatus : BaseCatchListViewModelTest
{
    public WhenTestingRefreshSyncStatus()
    {
        MockSessionCoordinator.IsCurrent(Arg.Any<AccountSessionLeaseModel>()).Returns(true);
    }

    [Fact]
    public async Task ItShouldReadNothingWhenNoAccountIsSignedIn()
    {
        // Arrange
        MockSessionCoordinator.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        await Sut.RefreshSyncStatusAsync(CancellationToken.None);

        // Assert
        await MockSyncStatusStore.DidNotReceive().ListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDiscardStatusesReadForAnAccountThatIsNoLongerCurrent()
    {
        // Arrange
        var item = LoadStoredCatch();
        var lease = new AccountSessionLeaseModel(OwnerUserId, 1, CancellationToken.None);
        MockSessionCoordinator.Capture().Returns(lease);
        MockSessionCoordinator.IsCurrent(lease).Returns(false);
        MockSyncStatusStore.ListAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CatchSyncStatusModel> { [item.Id] = Status(item.Id, CatchSyncStateEnum.Synced) });

        // Act
        await Sut.RefreshSyncStatusAsync(CancellationToken.None);

        // Assert
        item.SyncState.Should().Be(CatchSyncStateEnum.WaitingToSync);
        item.SaveStatus.Should().Be("Saved locally on this device.");
    }

    [Fact]
    public async Task ItShouldClearTheStatusOfACatchTheStoreNoLongerReports()
    {
        // Arrange
        var item = LoadStoredCatch();
        item.ApplySyncStatus(Status(item.Id, CatchSyncStateEnum.ActionRequired, canRetry: true));
        ArrangeStatuses(new Dictionary<Guid, CatchSyncStatusModel>());

        // Act
        await Sut.RefreshSyncStatusAsync(CancellationToken.None);

        // Assert
        item.CanRetrySync.Should().BeFalse();
        item.SaveStatus.Should().Be("Saved locally on this device.");
    }

    [Fact]
    public async Task ItShouldUpdateTheMatchingCatchInPlaceWithoutRebuildingTheList()
    {
        // Arrange
        var item = LoadStoredCatch();
        var other = LoadStoredCatch(Guid.NewGuid());
        var before = Sut.Catches;
        ArrangeStatuses(new Dictionary<Guid, CatchSyncStatusModel>
        {
            [item.Id] = Status(item.Id, CatchSyncStateEnum.NeedsAttention, canRetry: true)
        });

        // Act
        await Sut.RefreshSyncStatusAsync(CancellationToken.None);

        // Assert
        item.SyncState.Should().Be(CatchSyncStateEnum.NeedsAttention);
        item.CanRetrySync.Should().BeTrue();
        other.SyncState.Should().Be(CatchSyncStateEnum.WaitingToSync);
        Sut.Catches.Should().BeSameAs(before);
        await MockSyncStatusStore.Received(1).ListAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshWhenTheOutboxStateChanges()
    {
        // Arrange
        var item = LoadStoredCatch();
        var read = new TaskCompletionSource();
        MockSyncStatusStore.ListAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                read.TrySetResult();
                return Task.FromResult<IReadOnlyDictionary<Guid, CatchSyncStatusModel>>(
                    new Dictionary<Guid, CatchSyncStatusModel> { [item.Id] = Status(item.Id, CatchSyncStateEnum.Synced) });
            });

        // Act
        MockNotifier.StateChanged += Raise.Event();
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAsync(() => item.SyncState == CatchSyncStateEnum.Synced);

        // Assert
        item.SaveStatus.Should().Be("Synced.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Yield();
            timeout.Token.ThrowIfCancellationRequested();
        }
    }

    private CatchPresentationModel LoadStoredCatch(Guid? id = null)
    {
        var dto = new CatchDto(id ?? Guid.NewGuid(), new DateTimeOffset(2026, 6, 14, 7, 30, 0, TimeSpan.Zero), []);
        var item = new CatchPresentationModel(dto, Text, ["one.png"], savedLocally: true);
        StoredCatches.Add(item);
        Sut.LoadJournalEntries(StoredCatches.ToArray());
        return item;
    }

    private List<CatchPresentationModel> StoredCatches { get; } = [];

    private void ArrangeStatuses(IReadOnlyDictionary<Guid, CatchSyncStatusModel> statuses)
    {
        MockSyncStatusStore.ListAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(statuses);
    }

    private static CatchSyncStatusModel Status(Guid catchId, CatchSyncStateEnum state, bool canRetry = false)
    {
        return new CatchSyncStatusModel
        {
            CatchId = catchId,
            State = state,
            MetadataSynced = state == CatchSyncStateEnum.Synced,
            PhotographCount = 1,
            PhotographsRecorded = state == CatchSyncStateEnum.Synced ? 1 : 0,
            CanRetry = canRetry
        };
    }
}
