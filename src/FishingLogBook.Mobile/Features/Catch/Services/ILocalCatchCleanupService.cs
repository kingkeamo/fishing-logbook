namespace FishingLogBook.Mobile.Features.Catch.Services;

public interface ILocalCatchCleanupService
{
    void Start();

    Task RunAsync(CancellationToken cancellationToken);
}
