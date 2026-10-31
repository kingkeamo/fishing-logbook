namespace FishingLogBook.Mobile.Common.Offline.Services;

public interface IOutboxChangeNotifier
{
    event EventHandler? WorkEnqueued;

    event EventHandler? StateChanged;

    void NotifyWorkEnqueued();

    void NotifyStateChanged();
}
