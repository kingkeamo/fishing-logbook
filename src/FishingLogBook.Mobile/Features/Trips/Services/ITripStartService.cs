namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface ITripStartService
{
    Task<Guid?> StartAsync(CancellationToken cancellationToken);
}
