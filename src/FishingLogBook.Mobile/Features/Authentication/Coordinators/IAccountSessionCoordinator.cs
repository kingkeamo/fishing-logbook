using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Features.Authentication.Coordinators;

public interface IAccountSessionCoordinator
{
    AccountSessionLeaseModel? Capture();
    bool IsCurrent(AccountSessionLeaseModel lease);
    Task TransitionAsync(Guid? userId, CancellationToken cancellationToken);
    Task<bool> TryCommitAsync(
        AccountSessionLeaseModel lease,
        Func<CancellationToken, Task> commit,
        CancellationToken cancellationToken);
}
