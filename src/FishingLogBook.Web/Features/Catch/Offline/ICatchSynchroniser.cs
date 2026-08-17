namespace FishingLogBook.Web.Features.Catch.Offline;

public interface ICatchSynchroniser
{
    event EventHandler? StateChanged;

    Task SynchronisePendingAsync(CancellationToken cancellationToken);

    Task RetryAsync(Guid catchId, CancellationToken cancellationToken);
}
