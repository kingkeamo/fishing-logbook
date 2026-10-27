using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Popups;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Native.Controls;

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

    private void OnSpeciesDefault(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: SpeciesPreferenceEditModel species } && species.IsSelected)
        {
            var method = _viewModel.Methods.First(item => item.Id == species.MethodId);
            _viewModel.SetDefaultSpecies(method, species);
        }
    }

    private void OnSpeciesRemove(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: SpeciesPreferenceEditModel species })
        {
            var method = _viewModel.Methods.First(item => item.Id == species.MethodId);
            _viewModel.RemoveSpecies(method, species);
        }
    }

    private async void OnMore(object? sender, EventArgs args)
    {
        if (sender is not Button { BindingContext: PreferenceMoreActionModel action })
        {
            return;
        }

        if (action.Method is null)
        {
            var methods = await ShowPickerAsync(_viewModel.CreateMethodPicker());
            if (methods is not null)
            {
                _viewModel.ApplyMethodPicker(methods);
            }

            return;
        }

        var species = await ShowPickerAsync(_viewModel.CreateSpeciesPicker(action.Method));
        if (species is not null)
        {
            _viewModel.ApplySpeciesPicker(action.Method, species);
        }
    }

    private async Task<CataloguePickerResultModel?> ShowPickerAsync(CataloguePickerViewModel picker)
    {
        // Closing the popup raises OnAppearing on Android; reloading then would discard the picker's unsaved edits.
        _returningFromPicker = true;
        var result = await this.ShowPopupAsync<CataloguePickerResultModel>(
            new CataloguePickerPopup(picker),
            new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true });
        return result.WasDismissedByTappingOutsideOfPopup ? null : result.Result;
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

    private void OnRemoveLocation(object? sender, TappedEventArgs args)
    {
        if (sender is BindableObject { BindingContext: FishingLocationEditModel location })
        {
            _viewModel.RemoveLocation(location);
        }
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        await _viewModel.SaveAsync(CancellationToken.None);
    }
}
