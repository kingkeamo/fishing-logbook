using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Startup.Coordinators.StartupOperationCoordinatorTests;

public sealed class WhenTestingCancel : BaseStartupOperationCoordinatorTest
{
    [Fact]
    public async Task ItShouldKeepTheCancellationTokenUsableUntilTheOperationFinishes()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueAfterCancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registrationSucceeded = false;
        var operation = Sut.RestartAsync(async cancellationToken =>
        {
            started.SetResult();
            await continueAfterCancellation.Task;
            using var registration = cancellationToken.Register(() => { });
            registrationSucceeded = true;
            cancellationToken.ThrowIfCancellationRequested();
        });
        await started.Task;
        // Act
        Sut.Cancel();
        continueAfterCancellation.SetResult();
        var action = () => operation;
        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        registrationSucceeded.Should().BeTrue();
    }
}
