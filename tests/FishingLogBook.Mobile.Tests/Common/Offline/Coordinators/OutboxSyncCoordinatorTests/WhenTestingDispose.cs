using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Coordinators.OutboxSyncCoordinatorTests;

public sealed class WhenTestingDispose : BaseOutboxSyncCoordinatorTest
{
    [Fact]
    public void ItShouldBeSafeToDisposeTwice()
    {
        // Arrange
        Sut.Dispose();

        // Act
        var act = () => Sut.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ItShouldNotRunWhenRunAsyncIsCalledAfterDisposal()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        Sut.Dispose();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        MockSession.DidNotReceive().Capture();
    }

    [Fact]
    public async Task ItShouldDisposeThePendingRetryTimer()
    {
        // Arrange
        await SeedAsync(availableAt: Now.AddMinutes(1));
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Act
        Sut.Dispose();

        // Assert
        MockTimer.Received(1).Dispose();
    }

    [Fact]
    public void ItShouldStopReactingToLifecycleAndWorkEnqueuedEvents()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        Sut.Start();
        Sut.Dispose();

        // Act
        MockLifecycle.StateChanged += Raise.Event();
        MockNotifier.WorkEnqueued += Raise.Event();

        // Assert
        MockSession.DidNotReceive().Capture();
    }
}
