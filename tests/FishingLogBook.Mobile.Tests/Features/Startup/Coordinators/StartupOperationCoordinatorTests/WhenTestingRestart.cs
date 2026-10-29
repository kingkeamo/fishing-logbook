using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Startup.Coordinators.StartupOperationCoordinatorTests;

public sealed class WhenTestingRestart : BaseStartupOperationCoordinatorTest
{
    [Fact]
    public async Task ItShouldCancelThePreviousRunBeforeStartingTheRetry()
    {
        // Arrange
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = 0;
        var secondWasCancelled = false;
        // Act
        var first = Sut.RestartAsync(async cancellationToken =>
        {
            run++;
            firstStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        await firstStarted.Task;
        var second = Sut.RestartAsync(cancellationToken =>
        {
            secondWasCancelled = cancellationToken.IsCancellationRequested;
            run++;
            secondStarted.SetResult();
            return Task.CompletedTask;
        });
        var firstAction = () => first;
        // Assert
        await firstAction.Should().ThrowAsync<OperationCanceledException>();
        await secondStarted.Task;
        await second;
        run.Should().Be(2);
        secondWasCancelled.Should().BeFalse();
    }
}
