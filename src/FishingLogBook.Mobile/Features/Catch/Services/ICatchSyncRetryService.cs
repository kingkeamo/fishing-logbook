namespace FishingLogBook.Mobile.Features.Catch.Services;

public interface ICatchSyncRetryService
{
    Task RetryAsync(Guid catchId, CancellationToken cancellationToken);
}
