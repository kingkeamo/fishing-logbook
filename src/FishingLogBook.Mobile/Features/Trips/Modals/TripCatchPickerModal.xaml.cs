using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Native.Controls;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

public partial class TripCatchPickerModal : Popup<bool>
{
    private readonly TripCatchPickerViewModel _viewModel;
    private bool _closing;

    public TripCatchPickerModal(TripCatchPickerViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        WidthRequest = Math.Min(720, DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density);
    }

    private void OnToggle(object? sender, EventArgs args)
    {
        if (sender is ChipView { CommandParameter: string code })
        {
            _viewModel.Toggle(code);
        }
    }

    private async void OnConfirm(object? sender, EventArgs args)
    {
        if (await _viewModel.ConfirmAsync(CancellationToken.None))
        {
            await CompleteAsync(true);
        }
    }

    private async void OnCancel(object? sender, EventArgs args)
    {
        await CompleteAsync(false);
    }

    private async Task CompleteAsync(bool changed)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await CloseAsync(changed);
    }
}
