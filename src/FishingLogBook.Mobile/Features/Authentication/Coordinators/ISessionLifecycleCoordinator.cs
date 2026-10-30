using FishingLogBook.Mobile.Features.Authentication.Enums;

namespace FishingLogBook.Mobile.Features.Authentication.Coordinators;

public interface ISessionLifecycleCoordinator
{
    event EventHandler? StateChanged;
    SessionLifecycleStateEnum State { get; }
    void Start();
    Task ForegroundAsync(CancellationToken cancellationToken);
    void Background();
    Task RetryAsync(CancellationToken cancellationToken);
}
