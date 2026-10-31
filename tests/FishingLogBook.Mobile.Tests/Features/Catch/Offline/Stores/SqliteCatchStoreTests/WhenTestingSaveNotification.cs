using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchStoreTests;

public sealed class WhenTestingSaveNotification : BaseSqliteCatchStoreTest
{
    [Fact]
    public async Task ItShouldNotNotifyWhenSavingToAnotherRecordingUserIsForbidden()
    {
        // Arrange
        var args = CreateSaveArgs();
        var forbidden = new LocalCatchSaveArgs
        {
            Id = args.Id,
            RecordedByUserId = OtherUserId,
            CaughtOn = args.CaughtOn,
            Lifecycle = LocalCatchLifecycleEnum.Saved
        };

        // Act
        var result = await Sut.SaveAsync(forbidden, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocalCatchSaveOutcomeEnum.Forbidden);
        MockNotifier.DidNotReceive().NotifyWorkEnqueued();
    }

    [Fact]
    public async Task ItShouldNotNotifyWhenTheCatchBelongsToAnotherAccount()
    {
        // Arrange
        var args = CreateSaveArgs();
        (await CreateStoreForUser(OtherUserId).SaveAsync(args, CancellationToken.None)).IsSuccess.Should().BeTrue();
        MockNotifier.ClearReceivedCalls();

        // Act
        var result = await Sut.SaveAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        MockNotifier.DidNotReceive().NotifyWorkEnqueued();
    }

    [Fact]
    public async Task ItShouldNotNotifyWhenTheSaveIsInvalid()
    {
        // Arrange
        var args = CreateSaveArgs(catchId: Guid.Empty);

        // Act
        var result = await Sut.SaveAsync(args, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocalCatchSaveOutcomeEnum.InvalidRequest);
        MockNotifier.DidNotReceive().NotifyWorkEnqueued();
    }

    [Fact]
    public async Task ItShouldNotNotifyForADraftSave()
    {
        // Arrange
        var args = CreateSaveArgs(lifecycle: LocalCatchLifecycleEnum.Draft);

        // Act
        var result = await Sut.SaveAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        MockNotifier.DidNotReceive().NotifyWorkEnqueued();
    }

    [Fact]
    public async Task ItShouldNotifyOnceAfterASuccessfulSavedSave()
    {
        // Arrange
        var args = CreateSaveArgs();

        // Act
        var result = await Sut.SaveAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        MockNotifier.Received(1).NotifyWorkEnqueued();
        (await Sut.GetAsync(args.Id, CancellationToken.None)).Should().NotBeNull();
    }
}
