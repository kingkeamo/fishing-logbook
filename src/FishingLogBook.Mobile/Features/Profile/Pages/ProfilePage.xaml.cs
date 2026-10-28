using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.ViewModels;

namespace FishingLogBook.Mobile.Features.Profile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;
    private bool _returningFromPicker;

    public ProfilePage(ProfileViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel.Attach(action => Dispatcher.Dispatch(action));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_returningFromPicker)
        {
            _returningFromPicker = false;
            return;
        }

        await _viewModel.LoadAsync(CancellationToken.None);
    }

    private void OnPreferencePickerPresented(object? sender, EventArgs args)
    {
        _returningFromPicker = true;
    }

    private void OnAddLocation(object? sender, EventArgs args)
    {
        if (_viewModel.AddLocation(NewLocation.Text))
        {
            NewLocation.Text = string.Empty;
        }
    }

    private void OnLocationRenamed(object? sender, FocusEventArgs args)
    {
        if (sender is Entry { BindingContext: FishingLocationEditModel location } entry)
        {
            _viewModel.RenameLocation(location, entry.Text);
        }
    }

    private void OnLocationDefault(object? sender, TappedEventArgs args)
    {
        if (sender is BindableObject { BindingContext: FishingLocationEditModel location })
        {
            _viewModel.SetDefaultLocation(location);
        }
    }

    private async void OnRemoveLocation(object? sender, TappedEventArgs args)
    {
        if (sender is not BindableObject { BindingContext: FishingLocationEditModel location })
        {
            return;
        }

        if (location.IsDefault)
        {
            _returningFromPicker = true;
        }

        await _viewModel.RemoveLocationAsync(location);
    }

    private async void OnDiscardAndReload(object? sender, EventArgs args)
    {
        await _viewModel.DiscardEditsAndReloadAsync(CancellationToken.None);
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        await _viewModel.SaveAsync(CancellationToken.None);
    }
}
