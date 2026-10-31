using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingLoadFromStore : BaseCatchListViewModelTest
{
    [Fact]
    public async Task ItShouldShowEmptyJournalWhenNoSavedCatchesExist()
    {
        // Arrange
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        Sut.IsEmptyJournal.Should().BeTrue();
        Sut.Catches.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowLocalDraftsWithADraftStatus()
    {
        // Arrange
        var draft = CreateRecord(LocalCatchLifecycleEnum.Draft);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([draft]);
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        Sut.IsEmptyJournal.Should().BeFalse();
        var item = Sut.Catches.Should().ContainSingle().Subject;
        item.IsDraft.Should().BeTrue();
        item.SaveStatus.Should().Be(Text["CatchDraftStatus"]);
    }

    [Fact]
    public async Task ItShouldLoadSavedCatchesForTheSignedInAccount()
    {
        // Arrange
        var saved = CreateRecord(LocalCatchLifecycleEnum.Saved);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([saved]);
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        Sut.HasContent.Should().BeTrue();
        Sut.Catches.Should().ContainSingle(item => item.Id == saved.Id);
    }

    [Theory]
    [InlineData(CatchSyncStateEnum.WaitingToSync, false)]
    [InlineData(CatchSyncStateEnum.NeedsAttention, false)]
    [InlineData(CatchSyncStateEnum.Synced, true)]
    public async Task ItShouldDeduplicateByGuidAndPreferOnlyTheConfirmedServerVersion(CatchSyncStateEnum sync, bool remoteWins)
    {
        // Arrange
        var record = CreateRecord(LocalCatchLifecycleEnum.Saved);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([record]);
        SetStatus(record.Id, sync);
        var photo = "https://photos.example/preview";
        MockRemoteClient.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<CatchViewDto>>.Succeeded(
            [new CatchViewDto(record.Id, OwnerUserId, record.CaughtOn) { SpeciesName = "Server species", Photographs = [new(Guid.NewGuid(), "image/jpeg", photo)] }]));
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        var item = Sut.Catches.Should().ContainSingle().Subject;
        item.IsRemote.Should().Be(remoteWins);
        item.Species.Should().Be(remoteWins ? "Server species" : record.SpeciesName);
        if (remoteWins)
        {
            item.SelectedPhoto.Should().Be(photo);
        }
        await MockRemoteClient.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.Unavailable)]
    [InlineData(ApiOutcomeEnum.SessionExpired)]
    [InlineData(ApiOutcomeEnum.ServerError)]
    public async Task ItShouldNotTreatServiceFailureAsAnEmptyHistoryOrShowConfirmedLocalHistory(ApiOutcomeEnum outcome)
    {
        // Arrange
        var confirmed = CreateRecord(LocalCatchLifecycleEnum.Saved);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([confirmed]);
        SetStatus(confirmed.Id, CatchSyncStateEnum.Synced);
        MockRemoteClient.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<CatchViewDto>>.Failed(outcome));
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        Sut.IsEmptyJournal.Should().BeFalse();
        Sut.HasContent.Should().BeFalse();
        Sut.Catches.Should().BeEmpty();
        Sut.State.State.Should().Be(ContentStateEnum.Error);
        Sut.State.Message.Should().Be(Text[outcome == ApiOutcomeEnum.SessionExpired ? "CatchRemoteSessionExpiredBody" : "CatchRemoteUnavailableBody"]);
        Sut.RemoteNotice.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ItShouldShowOnlyPendingAndDraftRecordsOffline()
    {
        // Arrange
        var confirmed = CreateRecord(LocalCatchLifecycleEnum.Saved);
        var pending = CreateRecord(LocalCatchLifecycleEnum.Saved);
        var draft = CreateRecord(LocalCatchLifecycleEnum.Draft);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([confirmed, pending, draft]);
        SetStatus(confirmed.Id, CatchSyncStateEnum.Synced);
        MockConnectivity.IsOnline.Returns(false);
        // Act
        await Sut.LoadUnitsAsync(CancellationToken.None);
        // Assert
        Sut.Catches.Select(item => item.Id).Should().BeEquivalentTo([pending.Id, draft.Id]);
        Sut.RemoteState.Should().Be(RemoteCatchStateEnum.Offline);
        Sut.HasRemoteNotice.Should().BeTrue();
        await MockRemoteClient.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDiscardASlowResponseAfterAnAccountSwitch()
    {
        // Arrange
        var response = new TaskCompletionSource<ApiResultModel<List<CatchViewDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockRemoteClient.ListAsync(Arg.Any<CancellationToken>()).Returns(response.Task);
        var load = Sut.LoadUnitsAsync(CancellationToken.None);
        MockSessionCoordinator.IsCurrent(Arg.Any<FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel>()).Returns(false);
        // Act
        response.SetResult(ApiResultModel<List<CatchViewDto>>.Succeeded([new(Guid.NewGuid(), OwnerUserId, DateTimeOffset.UtcNow)]));
        await load;
        // Assert
        Sut.Catches.Should().BeEmpty();
    }

    private void SetStatus(Guid id, CatchSyncStateEnum state)
    {
        MockSyncStatusStore.ListAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, CatchSyncStatusModel>
        {
            [id] = new() { CatchId = id, State = state, MetadataSynced = state == CatchSyncStateEnum.Synced, PhotographCount = 0, PhotographsRecorded = 0, CanRetry = false }
        });
    }

    private static LocalCatchRecordModel CreateRecord(LocalCatchLifecycleEnum lifecycle)
    {
        return new LocalCatchRecordModel
        {
            Id = Guid.NewGuid(),
            UserId = BaseCatchListViewModelTest.OwnerUserId,
            CaughtByUserId = BaseCatchListViewModelTest.OwnerUserId,
            RecordedByUserId = BaseCatchListViewModelTest.OwnerUserId,
            CaughtOn = DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
            SpeciesName = "Brown trout",
            Method = "Fly",
            Revision = 1,
            Lifecycle = lifecycle,
            CreatedOn = DateTimeOffset.UtcNow,
            UpdatedOn = DateTimeOffset.UtcNow,
            Photographs = []
        };
    }
}
