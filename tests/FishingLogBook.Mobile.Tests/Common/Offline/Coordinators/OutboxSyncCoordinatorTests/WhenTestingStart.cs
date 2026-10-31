using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Coordinators.OutboxSyncCoordinatorTests;

public sealed class WhenTestingStart : BaseOutboxSyncCoordinatorTest
{
    [Fact]
    public void ItShouldNotRunAnythingBeforeStartIsCalled()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        MockLifecycle.StateChanged += Raise.Event();
        MockNotifier.WorkEnqueued += Raise.Event();

        // Assert
        MockSession.DidNotReceive().Capture();
    }

    [Fact]
    public void ItShouldRunOnceWhenTheLifecycleBecomesOnline()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        MockLifecycle.State.Returns(SessionLifecycleStateEnum.Online);
        Sut.Start();

        // Act
        MockLifecycle.StateChanged += Raise.Event();

        // Assert
        MockSession.Received(1).Capture();
    }

    [Fact]
    public void ItShouldRunOnceWhenWorkIsEnqueued()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        Sut.Start();

        // Act
        MockNotifier.WorkEnqueued += Raise.Event();

        // Assert
        MockSession.Received(1).Capture();
    }

    [Fact]
    public void ItShouldSubscribeOnlyOnceWhenStartIsCalledTwice()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        Sut.Start();
        Sut.Start();

        // Act
        MockLifecycle.StateChanged += Raise.Event();
        MockNotifier.WorkEnqueued += Raise.Event();

        // Assert
        MockSession.Received(2).Capture();
    }

    [Fact]
    public void ItShouldThrowWhenStartedAfterDisposal()
    {
        // Arrange
        Sut.Dispose();

        // Act
        var act = () => Sut.Start();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Theory]
    [InlineData(SessionLifecycleStateEnum.LocalOffline)]
    [InlineData(SessionLifecycleStateEnum.SignInRequired)]
    [InlineData(SessionLifecycleStateEnum.Recovering)]
    [InlineData(SessionLifecycleStateEnum.RecoverableError)]
    [InlineData(SessionLifecycleStateEnum.Idle)]
    public async Task ItShouldCancelTheRunningSynchronisationWithoutRunningAgainWhenTheLifecycleLeavesOnline(
        SessionLifecycleStateEnum state)
    {
        // Arrange
        var id = await SeedAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RespondWith(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return OutboxOperationResultModel.Completed();
        });
        Sut.Start();
        var run = Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);
        await started.Task;
        MockLifecycle.State.Returns(state);

        // Act
        MockLifecycle.StateChanged += Raise.Event();
        await run;

        // Assert
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == id),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        MockSession.Received(1).Capture();
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(0);
        row.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public void ItShouldRunOnceWhenTheAccountSessionIsActivated()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        Sut.Start();

        // Act
        MockSession.SessionActivated += Raise.Event();

        // Assert
        MockSession.Received(1).Capture();
    }

    [Fact]
    public void ItShouldNotRunWhenTheAccountSessionIsActivatedAfterDisposal()
    {
        // Arrange
        Sut.Start();
        Sut.Dispose();

        // Act
        MockSession.SessionActivated += Raise.Event();

        // Assert
        MockSession.DidNotReceive().Capture();
    }
}
