using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Mobile.Features.Measurements.Popups;

public partial class MeasurementEditorPopup : Popup<bool>
{
    private readonly MeasurementEditorViewModel _viewModel;
    private bool _closing;

    public MeasurementEditorPopup(MeasurementEditorViewModel viewModel, bool reduceMotion = false)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        Editor.ViewModel = viewModel;
        Editor.ReduceMotion = reduceMotion;
    }

    public static async Task<bool> ShowAsync(Page page, MeasurementEditorViewModel viewModel, bool reduceMotion = false)
    {
        var result = await page.ShowPopupAsync<bool>(
            new MeasurementEditorPopup(viewModel, reduceMotion),
            new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true });
        return !result.WasDismissedByTappingOutsideOfPopup && result.Result;
    }

    private void OnClear(object? sender, EventArgs args)
    {
        Editor.ResignFocus();
        _viewModel.Clear();
    }

    private async void OnApply(object? sender, EventArgs args)
    {
        Editor.ResignFocus();
        var result = _viewModel.TryApply(
            Editor.PendingExactText,
            Editor.PendingPoundsText,
            Editor.PendingOuncesText);
        if (!result.Succeeded)
        {
            await NotifyAsync(NotificationKindEnum.Error, result.ErrorMessage ?? _viewModel.Text["MeasureInvalid"]);
            return;
        }

        await CompleteAsync(true);
    }

    private async void OnDismiss(object? sender, EventArgs args)
    {
        await CompleteAsync(false);
    }

    private async Task CompleteAsync(bool accepted)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await CloseAsync(accepted);
    }

    private async Task NotifyAsync(NotificationKindEnum kind, string message)
    {
        var interactions = Handler?.MauiContext?.Services.GetService<InteractionService>()
            ?? Application.Current?.Handler?.MauiContext?.Services.GetService<InteractionService>();
        if (interactions is null)
        {
            return;
        }

        await interactions.NotifyAsync(kind, message);
    }
}
