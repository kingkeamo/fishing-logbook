using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.ViewModels;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

public partial class StartPlannedTripModal : Popup<PlannedTripChoiceModel>
{
    private readonly StartPlannedTripViewModel _viewModel;
    private bool _closing;

    public StartPlannedTripModal(StartPlannedTripViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        WidthRequest = Math.Min(480, (DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) - 40);
    }

    private async void OnSelect(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid tripId })
        {
            await CloseOnceAsync(_viewModel.Choose(tripId));
        }
    }

    private async void OnStartNew(object? sender, EventArgs args)
    {
        await CloseOnceAsync(_viewModel.StartNew());
    }

    private async void OnDismiss(object? sender, EventArgs args)
    {
        await CloseOnceAsync(PlannedTripChoiceModel.Dismissed);
    }

    private async Task CloseOnceAsync(PlannedTripChoiceModel choice)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await CloseAsync(choice);
    }
}
