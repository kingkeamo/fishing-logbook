using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Import.Modals.PlaceName;

public partial class PlaceNameModal : ComponentBase
{
    private IReadOnlyList<string> _components = [];
    private HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public PlaceNameModalModel Model { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string? Preview
    {
        get
        {
            var value = string.Join(", ", _components.Where(_selected.Contains));
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    private bool IsValid
    {
        get
        {
            return Preview is null || Preview.Length <= CatchDetailConstants.MaxPlaceNameLength;
        }
    }

    protected override void OnParametersSet()
    {
        _components = Model.Components
            .Where(component => !string.IsNullOrWhiteSpace(component))
            .Select(component => component.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var current = Model.CurrentPlaceName?.Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        _selected = current is null
            ? _components.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : current.Where(component => _components.Contains(component, StringComparer.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void SetSelected(string component, bool selected)
    {
        if (selected)
        {
            _selected.Add(component);
            return;
        }

        _selected.Remove(component);
    }

    private void Apply()
    {
        var placeName = Preview;
        if (placeName?.Length > CatchDetailConstants.MaxPlaceNameLength)
        {
            return;
        }

        MudDialog.Close(DialogResult.Ok(new PlaceNameModalResult(placeName)));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
