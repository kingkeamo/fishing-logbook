using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Native.Controls;

namespace FishingLogBook.Mobile.Features.Profile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

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
        await _viewModel.LoadAsync(CancellationToken.None);
    }

    private void OnMethodSelection(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: MethodPreferenceEditModel method })
        {
            _viewModel.ToggleMethod(method);
        }
    }

    private void OnMethodDefault(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: MethodPreferenceEditModel method } && method.IsSelected)
        {
            _viewModel.SetDefaultMethod(method);
        }
    }

    private void OnSpeciesSelection(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: SpeciesPreferenceEditModel species })
        {
            var method = _viewModel.Methods.First(item => item.Id == species.MethodId);
            _viewModel.ToggleSpecies(method, species);
        }
    }

    private void OnSpeciesDefault(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: SpeciesPreferenceEditModel species } && species.IsSelected)
        {
            var method = _viewModel.Methods.First(item => item.Id == species.MethodId);
            _viewModel.SetDefaultSpecies(method, species);
        }
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

    private void OnLocationDefault(object? sender, EventArgs args)
    {
        if (sender is Button { BindingContext: FishingLocationEditModel location })
        {
            _viewModel.SetDefaultLocation(location);
        }
    }

    private void OnRemoveLocation(object? sender, EventArgs args)
    {
        if (sender is Button { BindingContext: FishingLocationEditModel location })
        {
            _viewModel.RemoveLocation(location);
        }
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        await _viewModel.SaveAsync(CancellationToken.None);
    }
}
