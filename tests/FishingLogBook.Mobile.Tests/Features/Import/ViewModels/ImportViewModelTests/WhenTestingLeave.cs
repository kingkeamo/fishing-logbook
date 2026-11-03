using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingLeave : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldNotDeletePreparedBytesWhilePersistenceIsStillRunning()
    {
        // Arrange
        await ReachConfirmAsync();
        MockConnectivity.IsOnline.Returns(true);
        var started = new TaskCompletionSource();
        var hold = new TaskCompletionSource();
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(async call =>
            {
                started.SetResult();
                await hold.Task;
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return new ImportPersistenceResultModel([Guid.NewGuid()], [Guid.NewGuid()], 1, 0);
            });

        // Act
        var persist = Sut.ContinueAsync(CancellationToken.None);
        await started.Task;
        await MockPreparation.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
        hold.SetResult();
        await persist;

        // Assert
        await MockPersistence.Received(1).PersistAsync(
            Arg.Any<ImportBatchModel>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IProgress<ImportPersistenceProgressModel>?>());
    }

    [Fact]
    public async Task ItShouldCancelPersistenceThenClearBytesWhenLeavingTheWizard()
    {
        // Arrange
        await ReachConfirmAsync();
        var started = new TaskCompletionSource();
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(async call =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                return new ImportPersistenceResultModel([], [], 0, 0);
            });

        // Act
        var persist = Sut.ContinueAsync(CancellationToken.None);
        await started.Task;
        await Sut.LeaveAsync();
        await persist;

        // Assert
        Sut.IsPersisting.Should().BeFalse();
        Sut.IsSuccessStep.Should().BeFalse();
        await MockPreparation.Received().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotClearPreparedBytesAfterASuccessfulImport()
    {
        // Arrange
        await ReachConfirmAsync();
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(new ImportPersistenceResultModel([Guid.NewGuid()], [Guid.NewGuid()], 1, 0));
        await Sut.ContinueAsync(CancellationToken.None);
        MockPreparation.ClearReceivedCalls();

        // Act
        await Sut.LeaveAsync();

        // Assert
        Sut.IsSuccessStep.Should().BeTrue();
        await MockPreparation.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectASecondPersistWhileOneIsInFlight()
    {
        // Arrange
        await ReachConfirmAsync();
        var started = new TaskCompletionSource();
        var hold = new TaskCompletionSource<ImportPersistenceResultModel>();
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(_ =>
            {
                started.TrySetResult();
                return hold.Task;
            });

        // Act
        var first = Sut.ContinueAsync(CancellationToken.None);
        await started.Task;
        await Sut.ContinueAsync(CancellationToken.None);
        hold.SetResult(new ImportPersistenceResultModel([Guid.NewGuid()], [Guid.NewGuid()], 1, 0));
        await first;

        // Assert
        await MockPersistence.Received(1).PersistAsync(
            Arg.Any<ImportBatchModel>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IProgress<ImportPersistenceProgressModel>?>());
        Sut.IsSuccessStep.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRequireAnOnlineConnectionBeforePersisting()
    {
        // Arrange
        await ReachConfirmAsync();
        MockConnectivity.IsOnline.Returns(false);

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.Message.Should().Be(Text["Import_OnlineRequired"]);
        Sut.IsSuccessStep.Should().BeFalse();
        await MockPersistence.DidNotReceive().PersistAsync(
            Arg.Any<ImportBatchModel>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IProgress<ImportPersistenceProgressModel>?>());
    }

    [Fact]
    public async Task ItShouldAwaitPhotoPreparationBeforeClearingTemporaryFilesWhenLeaving()
    {
        // Arrange
        await ReachPhotoStepAsync();
        var events = new List<string>();
        var started = new TaskCompletionSource();
        HoldPreparationUntilCancelled(started, events);
        RecordClear(events);

        // Act
        var pick = Sut.PickPhotosAsync(CancellationToken.None);
        await started.Task;
        await MockPreparation.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
        await Sut.LeaveAsync();
        await pick;

        // Assert
        events.Should().Equal("prep-cancelled", "clear");
        Sut.Photos.Should().BeEmpty();
        Sut.IsBusy.Should().BeFalse();
        await MockPreparation.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotPublishPreparedPhotosAfterTheWizardHasBeenAbandoned()
    {
        // Arrange
        await ReachPhotoStepAsync();
        var started = new TaskCompletionSource();
        var hold = new TaskCompletionSource<IReadOnlyList<ImportSelectedPhotoModel>>();
        MockPreparation.PrepareSelectionAsync(
                Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.SetResult();
                return hold.Task;
            });

        // Act
        var pick = Sut.PickPhotosAsync(CancellationToken.None);
        await started.Task;
        var leave = Sut.LeaveAsync();
        hold.SetResult([ReadyPhoto()]);
        await leave;
        await pick;

        // Assert
        Sut.Photos.Should().BeEmpty();
        Sut.CanContinue.Should().BeFalse();
        await MockPreparation.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMakeRepeatedLeaveCallsSafeAndClearOnce()
    {
        // Arrange
        await ReachPhotoStepAsync();
        var started = new TaskCompletionSource();
        HoldPreparationUntilCancelled(started);

        // Act
        var pick = Sut.PickPhotosAsync(CancellationToken.None);
        await started.Task;
        var first = Sut.LeaveAsync();
        var second = Sut.LeaveAsync();
        await Task.WhenAll(first, second);
        await pick;
        await Sut.LeaveAsync();

        // Assert
        await MockPreparation.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        Sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldCancelThePhotoPickerWithoutStartingPreparation()
    {
        // Arrange
        await ReachPhotoStepAsync();
        var started = new TaskCompletionSource();
        var hold = new TaskCompletionSource<PhotographPickResultModel>();
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                started.SetResult();
                call.Arg<CancellationToken>().Register(() =>
                    hold.TrySetResult(PhotographPickResultModel.Cancelled));
                return hold.Task;
            });

        // Act
        var pick = Sut.PickPhotosAsync(CancellationToken.None);
        await started.Task;
        await Sut.LeaveAsync();
        await pick;

        // Assert
        await MockPreparation.DidNotReceive().PrepareSelectionAsync(
            Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
            Arg.Any<CancellationToken>());
        await MockPreparation.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowANewPhotoSelectionAfterReloadingTheWizard()
    {
        // Arrange
        await ReachPhotoStepAsync();
        await Sut.LeaveAsync();
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        MockMedia.ClearReceivedCalls();
        MockPreparation.ClearReceivedCalls();
        MockPreparation.PrepareSelectionAsync(
                Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
                Arg.Any<CancellationToken>())
            .Returns([ReadyPhoto()]);

        // Act
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Assert
        await MockMedia.Received(1).PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        Sut.IsBatchStep.Should().BeFalse();
        Sut.Photos.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectANewPhotoSelectionAfterLeaving()
    {
        // Arrange
        await ReachPhotoStepAsync();
        await Sut.LeaveAsync();
        MockMedia.ClearReceivedCalls();
        MockPreparation.ClearReceivedCalls();

        // Act
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Assert
        await MockMedia.DidNotReceive().PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await MockPreparation.DidNotReceive().PrepareSelectionAsync(
            Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
            Arg.Any<CancellationToken>());
    }

    private async Task ReachPhotoStepAsync()
    {
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.FromFiles(
            [
                new PhotographPickedFileModel { FullPath = "photo.jpg", ContentType = "image/jpeg" }
            ]));
    }

    private void HoldPreparationUntilCancelled(TaskCompletionSource started, List<string>? events = null)
    {
        MockPreparation.PrepareSelectionAsync(
                Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                started.SetResult();
                var pending = new TaskCompletionSource<IReadOnlyList<ImportSelectedPhotoModel>>();
                call.Arg<CancellationToken>().Register(() =>
                {
                    events?.Add("prep-cancelled");
                    pending.TrySetCanceled();
                });
                return pending.Task;
            });
    }

    private void RecordClear(List<string> events)
    {
        MockPreparation.ClearAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            events.Add("clear");
            return Task.CompletedTask;
        });
    }
}
