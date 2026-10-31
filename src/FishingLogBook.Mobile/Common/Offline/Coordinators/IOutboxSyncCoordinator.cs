using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Common.Offline.Coordinators;

public interface IOutboxSyncCoordinator
{
    void Start();

    void Request(OutboxSyncTriggerEnum trigger);

    Task RunAsync(OutboxSyncTriggerEnum trigger, CancellationToken cancellationToken);

    void Pause();
}
