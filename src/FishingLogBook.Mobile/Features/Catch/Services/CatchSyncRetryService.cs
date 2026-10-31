using FishingLogBook.Mobile.Common.Offline.Coordinators;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;

namespace FishingLogBook.Mobile.Features.Catch.Services;

public sealed class CatchSyncRetryService(
    IAccountSessionCoordinator sessionCoordinator,
    IOutboxStore outboxStore,
    IOutboxSyncCoordinator syncCoordinator,
    IOutboxChangeNotifier notifier,
    TimeProvider timeProvider) : ICatchSyncRetryService
{
    public async Task RetryAsync(Guid catchId, CancellationToken cancellationToken)
    {
        var lease = sessionCoordinator.Capture();
        if (lease is null)
        {
            return;
        }

        await outboxStore.ResetForRetryAsync(lease.UserId, catchId, timeProvider.GetUtcNow(), cancellationToken);
        notifier.NotifyStateChanged();
        syncCoordinator.Request(OutboxSyncTriggerEnum.Manual);
    }
}
