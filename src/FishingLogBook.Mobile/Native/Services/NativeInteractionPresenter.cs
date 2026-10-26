using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Native.Controls;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class NativeInteractionPresenter : IInteractionPresenter
{
    private readonly SemaphoreSlim _dialogGate = new(1, 1);
    private ISnackbar? _snackbar;

    public async Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        await _dialogGate.WaitAsync(cancellationToken);
        ISnackbar snackbar = null!;
        Task display = Task.CompletedTask;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DismissSnackbarAsync();
                var background = (Color)Application.Current!.Resources["Notification" + request.Kind];
                snackbar = Snackbar.Make(request.Message, actionButtonText: request.Dismiss,
                    duration: TimeSpan.FromSeconds(6), visualOptions: new SnackbarOptions
                    {
                        BackgroundColor = background,
                        TextColor = Colors.White,
                        ActionButtonTextColor = Colors.White,
                        CornerRadius = 12,
                        Font = Microsoft.Maui.Font.SystemFontOfSize(16)
                    });
                _snackbar = snackbar;
                SemanticScreenReader.Default.Announce(request.Message);
                display = snackbar.Show(cancellationToken);
            });
        }
        finally
        {
            _dialogGate.Release();
        }

        try
        {
            await display;
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (ReferenceEquals(_snackbar, snackbar))
                {
                    _snackbar = null;
                }
            });
        }
    }

    public async Task<bool> ShowDialogAsync(DialogRequest request, CancellationToken cancellationToken)
    {
        await _dialogGate.WaitAsync(cancellationToken);
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DismissSnackbarAsync();

                var popup = new InteractionPopup(request);
                var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                popup.Opened += (_, _) => opened.TrySetResult();
                var completion = Shell.Current.ShowPopupAsync<bool>(popup, new PopupOptions
                {
                    CanBeDismissedByTappingOutsideOfPopup = !request.IsDestructive,
                    PageOverlayColor = Color.FromArgb("#99102A43")
                }, CancellationToken.None);
                try
                {
                    var result = await completion.WaitAsync(cancellationToken);
                    return !result.WasDismissedByTappingOutsideOfPopup && result.Result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await Task.WhenAny(opened.Task, completion);
                    if (!completion.IsCompleted)
                    {
                        await popup.CloseAsync(false, CancellationToken.None);
                    }

                    await completion;
                    throw;
                }
            });
        }
        finally
        {
            _dialogGate.Release();
        }
    }

    private async Task DismissSnackbarAsync()
    {
        var snackbar = _snackbar;
        _snackbar = null;
        if (snackbar is not null)
        {
            await snackbar.Dismiss(CancellationToken.None);
        }
    }
}
