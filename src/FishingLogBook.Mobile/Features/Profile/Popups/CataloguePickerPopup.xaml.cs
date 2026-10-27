using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Native.Controls;

namespace FishingLogBook.Mobile.Features.Profile.Popups;

public partial class CataloguePickerPopup : Popup<CataloguePickerResultModel>
{
    private readonly CataloguePickerViewModel _viewModel;
    private bool _closing;

    public CataloguePickerPopup(CataloguePickerViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        Sheet.MaximumHeightRequest = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density * 0.9;
    }

    private void OnOption(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: CataloguePickerOptionModel option })
        {
            _viewModel.Toggle(option);
        }
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        await CompleteAsync(_viewModel.ToResult());
    }

    private async void OnCancel(object? sender, EventArgs args)
    {
        await CompleteAsync(null);
    }

    private async Task CompleteAsync(CataloguePickerResultModel? result)
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
