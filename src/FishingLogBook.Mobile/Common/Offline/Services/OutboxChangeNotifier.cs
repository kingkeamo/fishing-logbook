namespace FishingLogBook.Mobile.Common.Offline.Services;

public sealed class OutboxChangeNotifier : IOutboxChangeNotifier
{
    public event EventHandler? WorkEnqueued;

    public event EventHandler? StateChanged;

    public event EventHandler? SyncRunCompleted;

    public event EventHandler? CatchesChanged;

    public void NotifyWorkEnqueued()
    {
        WorkEnqueued?.Invoke(this, EventArgs.Empty);
        NotifyStateChanged();
    }

    public void NotifyStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifySyncRunCompleted()
    {
        SyncRunCompleted?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyCatchesChanged()
    {
        CatchesChanged?.Invoke(this, EventArgs.Empty);
    }
}
