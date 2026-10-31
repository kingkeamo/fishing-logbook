using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Measurements.Popups;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Popups;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Native.Controls;

namespace FishingLogBook.Mobile.Features.Catch.Components.RecordCatchEditor;

public partial class RecordCatchEditorView : ContentView
{
    public RecordCatchEditorView()
    {
        InitializeComponent();
    }

    public event EventHandler? SaveSucceeded;

    private CatchEditViewModel? Editor => BindingContext as CatchEditViewModel;

    private async void OnMethodChip(object? sender, EventArgs args)
    {
        if (sender is not ChipView { CommandParameter: CatchChipOptionModel option } || Editor is null)
        {
            return;
        }

        if (string.Equals(option.Code, CatchEditViewModel.MoreOptionCode, StringComparison.Ordinal))
        {
            var result = await ShowPickerAsync(Editor.CreateMethodPicker());
            if (result is not null)
            {
                Editor.ApplyMethodPicker(result);
            }

            return;
        }

        Editor.SelectMethod(option.Name);
    }

    private async void OnSpeciesChip(object? sender, EventArgs args)
    {
        if (sender is not ChipView { CommandParameter: CatchChipOptionModel option } || Editor is null)
        {
            return;
        }

        if (string.Equals(option.Code, CatchEditViewModel.MoreOptionCode, StringComparison.Ordinal))
        {
            var result = await ShowPickerAsync(Editor.CreateSpeciesPicker());
            if (result is not null)
            {
                Editor.ApplySpeciesPicker(result);
            }

            return;
        }

        Editor.SelectSpecies(option.Name);
    }

    private async void OnCaptureLocation(object? sender, EventArgs args)
    {
        if (Editor is null)
        {
            return;
        }

        await Editor.CaptureLocationAsync(CancellationToken.None);
    }

    private async void OnEditWeight(object? sender, EventArgs args)
    {
        await EditMeasurementAsync(Editor?.CreateWeightEditor(), editor => Editor?.ApplyWeightEditor(editor));
    }

    private async void OnEditLength(object? sender, EventArgs args)
    {
        await EditMeasurementAsync(Editor?.CreateLengthEditor(), editor => Editor?.ApplyLengthEditor(editor));
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        if (Editor is null)
        {
            return;
        }

        if (await Editor.SaveAsync(CancellationToken.None))
        {
            SaveSucceeded?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task EditMeasurementAsync(
        MeasurementEditorViewModel? editor,
        Action<MeasurementEditorViewModel>? apply)
    {
        var page = FindPage();
        if (page is null || editor is null || apply is null)
        {
            return;
        }

        if (await MeasurementEditorPopup.ShowAsync(page, editor))
        {
            apply(editor);
        }
    }

    private async Task<CataloguePickerResultModel?> ShowPickerAsync(CataloguePickerViewModel picker)
    {
        var page = FindPage();
        if (page is null)
        {
            return null;
        }

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
