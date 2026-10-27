using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Coordinators.AccountSessionCoordinatorTests;

public abstract class BaseAccountSessionCoordinatorTest : IDisposable
{
    protected readonly AccountSessionCoordinator Sut = new();

    public void Dispose()
    {
        Sut.Dispose();
        GC.SuppressFinalize(this);
    }

    protected async Task<TaskCompletionSource> HoldCommitAsync(AccountSessionLeaseModel lease)
    {
        var commitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Sut.TryCommitAsync(
            lease,
            async _ =>
            {
                commitStarted.SetResult();
                await releaseCommit.Task;
            },
            CancellationToken.None);
        await commitStarted.Task;
        return releaseCommit;
    }
}
