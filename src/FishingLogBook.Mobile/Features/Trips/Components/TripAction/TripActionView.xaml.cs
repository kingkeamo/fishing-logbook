using FishingLogBook.Mobile.Features.Trips.ViewModels;

namespace FishingLogBook.Mobile.Features.Trips.Components.TripAction;

/// <summary>The Catches page "Start fishing" button (shown only when no trip is active). The hosting page hands it the shared banner view-model.</summary>
public partial class TripActionView : ContentView
{
    public TripActionView()
    {
        InitializeComponent();
    }

    public ActiveTripBannerViewModel? ViewModel
    {
        get => BindingContext as ActiveTripBannerViewModel;
        set => BindingContext = value;
    }

    private async void OnTapped(object? sender, TappedEventArgs args)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.StartAndOpenAsync(CancellationToken.None);
        }
    }
}
