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
        WidthRequest = Math.Min(720, DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density);
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
        if (sender is not Button { CommandParameter: Guid userId })
        {
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        var text = _viewModel.Text;
        if (page is not null && await page.DisplayAlertAsync(text["TripRemoveParticipantTitle"], text["TripRemoveParticipantMessage"], text["TripRemoveParticipant"], text["Cancel"]))
        {
            await _viewModel.RemoveAsync(userId, CancellationToken.None);
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
