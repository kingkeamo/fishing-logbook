namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface ILocalTripCleanupService
{
    void Start();

    Task RunAsync(CancellationToken cancellationToken);
}
