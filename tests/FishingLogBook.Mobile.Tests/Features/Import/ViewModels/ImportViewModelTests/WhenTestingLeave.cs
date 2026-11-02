using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;
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
}
