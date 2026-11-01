using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Features.Profile.Popups;
using FishingLogBook.Mobile.Features.Profile.ViewModels;

namespace FishingLogBook.Mobile.Features.Profile.Views;

public partial class HeaderProfileView : ContentView
{
    public HeaderProfileView()
    {
        InitializeComponent();
    }

    public HeaderProfileViewModel? ViewModel
    {
        get => BindingContext as HeaderProfileViewModel;
        set => BindingContext = value;
    }

    private async void OnTapped(object? sender, TappedEventArgs args)
    {
        if (ViewModel is not { DisplayName.Length: > 0 } viewModel || Shell.Current is null)
        {
            return;
        }

        await Shell.Current.ShowPopupAsync<bool>(new DisplayNamePopup(viewModel.DisplayName), new PopupOptions
        {
            PageOverlayColor = Color.FromArgb("#66102A43"),
            Shape = null
        });
    }
}
