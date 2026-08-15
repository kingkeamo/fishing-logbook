namespace FishingLogBook.Web.Models;

public enum SyncStatus
{
    SavedLocally,
    WaitingToSynchronise,
    Synchronising,
    Synchronised,
    FailedToSynchronise
}
