namespace FishingLogBook.Mobile.Common.Offline.Services;

public sealed class OutboxChangeNotifier : IOutboxChangeNotifier
{
    public event EventHandler? WorkEnqueued;

    public event EventHandler? StateChanged;

    public void NotifyWorkEnqueued()
    {
        WorkEnqueued?.Invoke(this, EventArgs.Empty);
        NotifyStateChanged();
    }

    public void NotifyStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
