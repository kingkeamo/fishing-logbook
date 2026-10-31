namespace FishingLogBook.Mobile.Common.Offline.Services;

public interface IOutboxChangeNotifier
{
    event EventHandler? WorkEnqueued;

    event EventHandler? StateChanged;

    event EventHandler? SyncRunCompleted;

    event EventHandler? CatchesChanged;

    void NotifyWorkEnqueued();

    void NotifyStateChanged();

    void NotifySyncRunCompleted();

    void NotifyCatchesChanged();
}
