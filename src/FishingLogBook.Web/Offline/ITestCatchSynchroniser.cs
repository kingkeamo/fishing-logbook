namespace FishingLogBook.Web.Offline;

public interface ITestCatchSynchroniser
{
    Task SynchronisePendingAsync(CancellationToken cancellationToken);

    Task RetryAsync(Guid id, CancellationToken cancellationToken);
}
