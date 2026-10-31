using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;

namespace FishingLogBook.Mobile.Features.Measurements.Popups;

public partial class MeasurementEditorPopup : Popup<bool>
{
    private bool _closing;

    public MeasurementEditorPopup(MeasurementEditorViewModel viewModel, bool reduceMotion = false)
    {
        InitializeComponent();
        BindingContext = viewModel;
        Editor.ViewModel = viewModel;
        Editor.ReduceMotion = reduceMotion;
        Sheet.MaximumHeightRequest = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density * 0.9;
    }

    public static async Task<bool> ShowAsync(Page page, MeasurementEditorViewModel viewModel, bool reduceMotion = false)
    {
        var result = await page.ShowPopupAsync<bool>(
            new MeasurementEditorPopup(viewModel, reduceMotion),
            new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true });
        return !result.WasDismissedByTappingOutsideOfPopup && result.Result;
    }

    private async void OnDone(object? sender, EventArgs args)
    {
        await CompleteAsync(true);
    }

    private async void OnCancel(object? sender, EventArgs args)
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
}
