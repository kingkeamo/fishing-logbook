using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingPersistenceOutcomes : BaseImportCatchActionsTest
{
    private void GivenPersistenceReturns(ImportPersistenceResultModel result)
    {
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(result);
    }

    [Fact]
    public async Task ItShouldNotPersistWhileOffline()
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

    [Theory]
    [InlineData(ImportPersistenceFailureEnum.Trip, "Import_TripPersistenceFailed")]
    [InlineData(ImportPersistenceFailureEnum.Catch, "Import_CatchPersistenceFailed")]
    [InlineData(ImportPersistenceFailureEnum.Photograph, "Import_PhotographPersistenceFailed")]
    [InlineData(ImportPersistenceFailureEnum.Verification, "Import_VerificationFailed")]
    public async Task ItShouldExplainWhichPartOfThePersistenceFailed(
        ImportPersistenceFailureEnum failure,
        string expectedKey)
    {
        // Arrange
        await ReachConfirmAsync();
        GivenPersistenceReturns(ImportPersistenceResultModel.Failed(failure, new InvalidOperationException("server")));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.Message.Should().Be(Text[expectedKey]);
        Sut.IsSuccessStep.Should().BeFalse();
        Sut.IsPersisting.Should().BeFalse();
        await MockPreparation.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAGenericFailureWhenPersistenceThrows()
    {
        // Arrange
        await ReachConfirmAsync();
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns<Task<ImportPersistenceResultModel>>(_ => throw new InvalidOperationException("boom"));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.Message.Should().Be(Text["Import_PersistenceFailed"]);
        Sut.IsPersisting.Should().BeFalse();
        Sut.IsSuccessStep.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldClearTheMessageWhenPersistenceIsCancelledByTheCaller()
    {
        // Arrange
        await ReachConfirmAsync();
        using var cts = new CancellationTokenSource();
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns<Task<ImportPersistenceResultModel>>(call =>
            {
                cts.Cancel();
                throw new OperationCanceledException(call.Arg<CancellationToken>());
            });

        // Act
        await Sut.ContinueAsync(cts.Token);

        // Assert
        Sut.HasMessage.Should().BeFalse();
        Sut.IsSuccessStep.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldSucceedClearPreparedBytesAndPointAtTheOnlyCreatedTrip()
    {
        // Arrange
        await ReachConfirmAsync();
        var tripId = Guid.NewGuid();
        GivenPersistenceReturns(new ImportPersistenceResultModel([tripId], [Guid.NewGuid()], 1, 0));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.IsSuccessStep.Should().BeTrue();
        Sut.IsConfirmStep.Should().BeFalse();
        Sut.DestinationTripId.Should().Be(tripId);
        Sut.CanGoBack.Should().BeFalse();
        Sut.ContinueText.Should().Be(Text["Import_ViewCatches"]);
        Sut.StepDescription.Should().NotBe(Text["Import_NotSaved"]);
        await MockPreparation.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotPointAtATripWhenSeveralWereCreated()
    {
        // Arrange
        await ReachConfirmAsync();
        GivenPersistenceReturns(new ImportPersistenceResultModel(
            [Guid.NewGuid(), Guid.NewGuid()],
            [Guid.NewGuid()],
            1,
            0));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.IsSuccessStep.Should().BeTrue();
        Sut.DestinationTripId.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotPointAtATripWhenNoneWasCreated()
    {
        // Arrange
        await ReachConfirmAsync();
        GivenPersistenceReturns(new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.IsSuccessStep.Should().BeTrue();
        Sut.DestinationTripId.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldDoNothingWhenContinuingAfterTheImportWasSaved()
    {
        // Arrange
        await ReachConfirmAsync();
        GivenPersistenceReturns(new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0));
        await Sut.ContinueAsync(CancellationToken.None);

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        await MockPersistence.Received(1).PersistAsync(
            Arg.Any<ImportBatchModel>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IProgress<ImportPersistenceProgressModel>?>());
    }

    [Fact]
    public async Task ItShouldNotClearPreparedBytesAgainWhenLeavingAfterASuccessfulImport()
    {
        // Arrange
        await ReachConfirmAsync();
        GivenPersistenceReturns(new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0));
        await Sut.ContinueAsync(CancellationToken.None);
        MockPreparation.ClearReceivedCalls();

        // Act
        await Sut.LeaveAsync();

        // Assert
        await MockPreparation.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearPreparedBytesOnceWhenLeavingBeforeSavingAndShareTheSameLeave()
    {
        // Arrange
        await ReachConfirmAsync();

        // Act
        var first = Sut.LeaveAsync();
        var second = Sut.LeaveAsync();
        await Task.WhenAll(first, second);

        // Assert
        await MockPreparation.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportProgressWhilePersisting()
    {
        // Arrange
        await ReachConfirmAsync();
        MockPersistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>?>())
            .Returns(call =>
            {
                call.Arg<IProgress<ImportPersistenceProgressModel>>()
                    .Report(new ImportPersistenceProgressModel(ImportPersistenceStageEnum.SavingCatch, 1, 2));
                return new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0);
            });
        var raised = new List<string?>();
        Sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        raised.Should().Contain(nameof(Sut.ProgressText));
        Sut.IsSuccessStep.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldStopTheTripReviewContinueWhenTheBatchIsNoLongerReady()
    {
        // Arrange
        await ReachTripReviewAsync();

        // Act
        var canContinue = Sut.CanContinue;

        // Assert
        canContinue.Should().BeFalse();
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.Stage.Should().Be(ImportStageEnum.ReviewTrips);
    }
}
