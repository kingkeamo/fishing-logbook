using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.ViewModels;

namespace FishingLogBook.Mobile.Features.Import.Modals;

public partial class PlaceNameModal : Popup<PlaceNameModalResult>
{
    private readonly PlaceNameModalViewModel _viewModel;
    private bool _closing;

    public PlaceNameModal(PlaceNameModalViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        WidthRequest = Math.Min(480, (DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) - 40);
    }

    private async void OnApply(object? sender, EventArgs args)
    {
        if (!_viewModel.CanApply)
        {
            return;
        }

        await CloseOnceAsync(_viewModel.ToResult());
    }

    private async void OnCancel(object? sender, EventArgs args)
    {
        await CloseOnceAsync(null);
    }

    private async Task CloseOnceAsync(PlaceNameModalResult? result)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        if (result is null)
        {
            await CloseAsync();
            return;
        }

        await CloseAsync(result);
    }
}
