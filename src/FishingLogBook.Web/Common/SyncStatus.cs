namespace FishingLogBook.Web.Common;

public enum SyncStatus
{
    SavedLocally,
    WaitingToSynchronise,
    Synchronising,
    Synchronised,
    FailedToSynchronise
}
