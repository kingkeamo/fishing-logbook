using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Popups;
using FishingLogBook.Mobile.Features.Profile.ViewModels;

namespace FishingLogBook.Mobile.Native.Controls;

public partial class FishingPreferenceEditorView : ContentView
{
    public static readonly BindableProperty ShowPrivacySwitchesProperty = BindableProperty.Create(
        nameof(ShowPrivacySwitches), typeof(bool), typeof(FishingPreferenceEditorView), true);

    public FishingPreferenceEditorView()
    {
        InitializeComponent();
    }

    public bool ShowPrivacySwitches
    {
        get => (bool)GetValue(ShowPrivacySwitchesProperty);
        set => SetValue(ShowPrivacySwitchesProperty, value);
    }

    public event EventHandler? PickerPresented;

    private ProfileViewModel? Editor => BindingContext as ProfileViewModel;

    private void OnMethodSelection(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: MethodPreferenceEditModel method })
        {
            Editor?.ToggleMethod(method);
        }
    }

    private void OnMethodDefault(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: MethodPreferenceEditModel method } && method.IsSelected)
        {
            Editor?.SetDefaultMethod(method);
        }
    }

    private void OnSpeciesDefault(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: SpeciesPreferenceEditModel species } &&
            species.IsSelected &&
            Editor is not null)
        {
            var method = Editor.Methods.First(item => item.Id == species.MethodId);
            Editor.SetDefaultSpecies(method, species);
        }
    }

    private void OnSpeciesRemove(object? sender, EventArgs args)
    {
        if (sender is SelectionPillView { BindingContext: SpeciesPreferenceEditModel species } &&
            Editor is not null)
        {
            var method = Editor.Methods.First(item => item.Id == species.MethodId);
            Editor.RemoveSpecies(method, species);
        }
    }

    private async void OnMore(object? sender, EventArgs args)
    {
        if (sender is not Button { BindingContext: PreferenceMoreActionModel action } || Editor is null)
        {
            return;
        }

        if (action.Method is null)
        {
            var methods = await ShowPickerAsync(Editor.CreateMethodPicker());
            if (methods is not null)
            {
                Editor.ApplyMethodPicker(methods);
            }

            return;
        }

        var species = await ShowPickerAsync(Editor.CreateSpeciesPicker(action.Method));
        if (species is not null)
        {
            Editor.ApplySpeciesPicker(action.Method, species);
        }
    }

    private async Task<CataloguePickerResultModel?> ShowPickerAsync(CataloguePickerViewModel picker)
    {
        var page = FindPage();
        if (page is null)
        {
            return null;
        }

        PickerPresented?.Invoke(this, EventArgs.Empty);
        var result = await page.ShowPopupAsync<CataloguePickerResultModel>(
            new CataloguePickerPopup(picker),
            new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true });
        return result.WasDismissedByTappingOutsideOfPopup ? null : result.Result;
    }

    private Page? FindPage()
    {
        Element? current = this;
        while (current is not null)
        {
            if (current is Page page)
            {
                return page;
            }

            current = current.Parent;
        }

        return Window?.Page ?? Application.Current?.Windows.FirstOrDefault()?.Page;
    }
}
