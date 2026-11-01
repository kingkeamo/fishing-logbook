using FishingLogBook.Mobile.Features.Trips.ViewModels;

namespace FishingLogBook.Mobile.Features.Trips.Components.TripParticipants;

public partial class TripParticipantsView : ContentView
{
    public TripParticipantsView()
    {
        InitializeComponent();
    }

    public TripParticipantsViewModel? ViewModel
    {
        get => BindingContext as TripParticipantsViewModel;
        set => BindingContext = value;
    }

    public async Task LoadAsync(Guid tripId)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.LoadAsync(tripId, CancellationToken.None);
        }
    }

    private async void OnRemove(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid userId } && ViewModel is { } viewModel)
        {
            await viewModel.ConfirmAndRemoveAsync(userId, CancellationToken.None);
        }
    }

    private async void OnInvite(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid userId } && ViewModel is { } viewModel)
        {
            await viewModel.InviteAsync(userId, CancellationToken.None);
        }
    }

    private void OnBeginInvite(object? sender, EventArgs args)
    {
        ViewModel?.BeginInvite();
    }

    private void OnEndInvite(object? sender, EventArgs args)
    {
        ViewModel?.EndInvite();
    }

    private async void OnSearch(object? sender, EventArgs args)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.SearchAsync(CancellationToken.None);
        }
    }

    private async void OnRetry(object? sender, EventArgs args)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.RetryAsync(CancellationToken.None);
        }
    }
}
