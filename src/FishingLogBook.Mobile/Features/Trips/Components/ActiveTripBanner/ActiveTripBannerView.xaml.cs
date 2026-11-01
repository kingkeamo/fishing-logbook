using FishingLogBook.Mobile.Features.Trips.ViewModels;

namespace FishingLogBook.Mobile.Features.Trips.Components.ActiveTripBanner;

/// <summary>The "fishing trip in progress" bar. The hosting page hands it the shared banner view-model.</summary>
public partial class ActiveTripBannerView : ContentView
{
    public ActiveTripBannerView()
    {
        InitializeComponent();
    }

    public ActiveTripBannerViewModel? ViewModel
    {
        get => BindingContext as ActiveTripBannerViewModel;
        set => BindingContext = value;
    }

    private async void OnView(object? sender, EventArgs args)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ViewTripAsync();
        }
    }

    private async void OnUpdate(object? sender, EventArgs args)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.UpdateTripAsync();
        }
    }
}
