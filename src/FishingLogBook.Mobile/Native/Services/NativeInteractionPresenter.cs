using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Native.Controls;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class NativeInteractionPresenter : IInteractionPresenter
{
    private ISnackbar? _snackbar;

    public async Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var background = (Color)Application.Current!.Resources["Notification" + request.Kind];
            _snackbar = Snackbar.Make(request.Message, actionButtonText: request.Dismiss,
                duration: TimeSpan.FromSeconds(6), visualOptions: new SnackbarOptions
                {
                    BackgroundColor = background,
                    TextColor = Colors.White,
                    ActionButtonTextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(16)
                });
            SemanticScreenReader.Default.Announce(request.Message);
            await _snackbar.Show(cancellationToken);
        });
    }

    public async Task<bool> ShowDialogAsync(DialogRequest request, CancellationToken cancellationToken)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_snackbar is not null)
            {
                await _snackbar.Dismiss(CancellationToken.None);
            }

            var popup = new InteractionPopup(request);
            var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            popup.Opened += (_, _) => opened.TrySetResult();
            var completion = Shell.Current.ShowPopupAsync<bool>(popup, new PopupOptions
            {
                CanBeDismissedByTappingOutsideOfPopup = !request.IsDestructive,
                Shape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = 16,
                    StrokeThickness = 0
                },
                PageOverlayColor = Color.FromArgb("#990B1F3A")
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
}
