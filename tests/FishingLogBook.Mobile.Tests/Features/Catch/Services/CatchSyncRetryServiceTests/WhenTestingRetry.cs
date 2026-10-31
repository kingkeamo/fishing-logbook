using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Services.CatchSyncRetryServiceTests;

public sealed class WhenTestingRetry : BaseCatchSyncRetryServiceTest
{
    [Fact]
    public async Task ItShouldDoNothingWhenNotSignedIn()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        await Sut.RetryAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await MockOutboxStore.DidNotReceive().ResetForRetryAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        MockNotifier.DidNotReceive().NotifyStateChanged();
        MockSyncCoordinator.DidNotReceive().Request(Arg.Any<OutboxSyncTriggerEnum>());
    }

    [Fact]
    public async Task ItShouldResetNotifyAndRequestSynchronisationInOrderWhenSignedIn()
    {
        // Arrange
        SignIn();
        var catchId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        // Act
        await Sut.RetryAsync(catchId, cts.Token);

        // Assert
        Received.InOrder(() =>
        {
            MockOutboxStore.ResetForRetryAsync(UserId, catchId, Now, cts.Token);
            MockNotifier.NotifyStateChanged();
            MockSyncCoordinator.Request(OutboxSyncTriggerEnum.Manual);
        });
        await MockOutboxStore.Received(1).ResetForRetryAsync(UserId, catchId, Now, cts.Token);
        MockNotifier.Received(1).NotifyStateChanged();
        MockSyncCoordinator.Received(1).Request(OutboxSyncTriggerEnum.Manual);
    }

    [Fact]
    public async Task ItShouldResetAndRequestSynchronisationOnEveryRepeatedRetry()
    {
        // Arrange
        SignIn();
        var firstCatch = Guid.NewGuid();
        var secondCatch = Guid.NewGuid();

        // Act
        await Sut.RetryAsync(firstCatch, CancellationToken.None);
        await Sut.RetryAsync(firstCatch, CancellationToken.None);
        await Sut.RetryAsync(secondCatch, CancellationToken.None);

        // Assert
        await MockOutboxStore.Received(2).ResetForRetryAsync(UserId, firstCatch, Now, Arg.Any<CancellationToken>());
        await MockOutboxStore.Received(1).ResetForRetryAsync(UserId, secondCatch, Now, Arg.Any<CancellationToken>());
        MockNotifier.Received(3).NotifyStateChanged();
        MockSyncCoordinator.Received(3).Request(OutboxSyncTriggerEnum.Manual);
    }
}
