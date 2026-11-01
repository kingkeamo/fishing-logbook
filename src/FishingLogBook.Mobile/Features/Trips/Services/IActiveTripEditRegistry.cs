namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface IActiveTripEditRegistry
{
    event EventHandler? EditReleased;

    Task<IDisposable> AcquireAsync(Guid tripId, CancellationToken cancellationToken);

    bool IsActive(Guid tripId);

    Task<bool> RunIfInactiveAsync(Guid? tripId, Func<Task<bool>> action, CancellationToken cancellationToken);
}
