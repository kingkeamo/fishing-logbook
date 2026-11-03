using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.ViewModels;

namespace FishingLogBook.Mobile.Features.Import.Modals;

public partial class ExistingTripModal : Popup<ExistingTripItemModel>
{
    private bool _closing;

    public ExistingTripModal(ExistingTripModalViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        WidthRequest = Math.Min(480, (DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) - 40);
    }

    private async void OnSelect(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid tripId } && BindingContext is ExistingTripModalViewModel viewModel)
        {
            var item = viewModel.Items.FirstOrDefault(candidate => candidate.TripId == tripId);
            if (item is not null)
            {
                await CloseOnceAsync(item);
            }
        }
    }

    private async void OnDismiss(object? sender, EventArgs args)
    {
        await CloseOnceAsync(null);
    }

    private async Task CloseOnceAsync(ExistingTripItemModel? item)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        if (item is null)
        {
            await CloseAsync();
            return;
        }

        await CloseAsync(item);
    }
}
