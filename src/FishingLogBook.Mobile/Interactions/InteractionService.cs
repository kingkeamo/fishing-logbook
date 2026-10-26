using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Interactions;

public sealed class InteractionService(IInteractionPresenter presenter, UiTextService text)
{
    private readonly SemaphoreSlim _dialog = new(1, 1);

    public async Task NotifyAsync(NotificationKindEnum kind, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await presenter.NotifyAsync(new NotificationRequest(kind, $"{text[kind.ToString()]} · {message}", text["Dismiss"]), cancellationToken);
    }

    public async Task<bool> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Accept);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Cancel);
        if (!await _dialog.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            return await presenter.ShowDialogAsync(request, cancellationToken);
        }
        finally
        {
            _dialog.Release();
        }
    }

    public async Task<bool> ShowModalAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        return await ConfirmAsync(new DialogRequest(title, message, text["Done"], text["Cancel"]), cancellationToken);
    }
}
