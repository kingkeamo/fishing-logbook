using FishingLogBook.Web.Features.TestCatch.Models;
namespace FishingLogBook.Web.Features.TestCatch.Offline.Synchronisers;

public interface ITestCatchSynchroniser
{
    Task SynchronisePendingAsync(CancellationToken cancellationToken);

    Task RetryAsync(Guid id, CancellationToken cancellationToken);

    Task RetryPhotographAsync(Guid id, CancellationToken cancellationToken);
}
