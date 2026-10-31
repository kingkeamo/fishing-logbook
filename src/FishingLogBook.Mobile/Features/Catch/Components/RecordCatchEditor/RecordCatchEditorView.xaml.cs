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

        if (Editor.ChooseMethod(option))
        {
            Editor.CompleteMethodPicker(await ShowPickerAsync(Editor.CreateMethodPicker()));
        }
    }

    private async void OnSpeciesChip(object? sender, EventArgs args)
    {
        if (sender is not ChipView { CommandParameter: CatchChipOptionModel option } || Editor is null)
        {
            return;
        }

        if (Editor.ChooseSpecies(option))
        {
            Editor.CompleteSpeciesPicker(await ShowPickerAsync(Editor.CreateSpeciesPicker()));
        }
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
        await EditMeasurementAsync(Editor?.CreateWeightEditor(), (applied, editor) => Editor?.CompleteWeightEdit(applied, editor));
    }

    private async void OnEditLength(object? sender, EventArgs args)
    {
        await EditMeasurementAsync(Editor?.CreateLengthEditor(), (applied, editor) => Editor?.CompleteLengthEdit(applied, editor));
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
        Action<bool, MeasurementEditorViewModel> complete)
    {
        var page = FindPage();
        if (page is null || editor is null)
        {
            return;
        }

        complete(await MeasurementEditorPopup.ShowAsync(page, editor), editor);
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
