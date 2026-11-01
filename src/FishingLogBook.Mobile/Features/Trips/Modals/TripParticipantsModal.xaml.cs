using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Trips.ViewModels;

namespace FishingLogBook.Mobile.Features.Trips.Modals;

public partial class TripParticipantsModal : Popup<bool>
{
    private readonly TripParticipantsViewModel _viewModel;
    private bool _closing;

    public TripParticipantsModal(TripParticipantsViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        WidthRequest = Math.Min(480, (DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) - 40);
    }

    private async void OnSearch(object? sender, EventArgs args)
    {
        await _viewModel.SearchAsync(CancellationToken.None);
    }

    private async void OnInvite(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid userId })
        {
            await _viewModel.InviteAsync(userId, CancellationToken.None);
        }
    }

    private async void OnRemove(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid userId })
        {
            await _viewModel.ConfirmAndRemoveAsync(userId, CancellationToken.None);
        }
    }

    private async void OnDone(object? sender, EventArgs args)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await CloseAsync(_viewModel.Changed);
    }
}
