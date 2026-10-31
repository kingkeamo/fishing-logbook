namespace FishingLogBook.Mobile.Features.Catch.Services;

public interface IActiveCatchEditRegistry
{
    event EventHandler? EditReleased;

    Task<IDisposable> AcquireAsync(Guid catchId, CancellationToken cancellationToken);

    bool IsActive(Guid catchId);

    Task<bool> RunIfInactiveAsync(Guid? catchId, Func<Task<bool>> action, CancellationToken cancellationToken);
}
