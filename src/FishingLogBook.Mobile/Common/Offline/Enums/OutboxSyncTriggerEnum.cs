namespace FishingLogBook.Mobile.Common.Offline.Enums;

public enum OutboxSyncTriggerEnum
{
    SessionOnline = 0,
    Foreground = 1,
    WorkEnqueued = 2,
    Manual = 3,
    RetryDue = 4
}
