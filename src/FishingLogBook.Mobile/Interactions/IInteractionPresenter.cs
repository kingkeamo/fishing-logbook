namespace FishingLogBook.Mobile.Interactions;

public interface IInteractionPresenter
{
    Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken);
    Task<bool> ShowDialogAsync(DialogRequest request, CancellationToken cancellationToken);
}
