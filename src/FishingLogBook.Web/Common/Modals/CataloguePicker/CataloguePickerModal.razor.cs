using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Common.Modals;

public partial class CataloguePickerModal : ComponentBase
{
    private const int InitialOptionLimit = 20;

    private string? _search;
    private HashSet<Guid> _selectedOptionIds = [];

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public CataloguePickerModalModel Model { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _selectedOptionIds = Model.SelectedOptionIds?.ToHashSet() ?? [];
    }

    private IReadOnlyList<CatalogueOptionModel> FilteredOptions
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return Model.AllowMultiple
                    ? [.. Model.Options.Take(InitialOptionLimit)]
                    : Model.Options;
            }

            var term = _search.Trim();
            return [.. Model.Options.Where(option =>
                option.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))];
        }
    }

    private IReadOnlyList<CatalogueOptionModel> SelectedOptions =>
        [.. Model.Options.Where(option => _selectedOptionIds.Contains(option.Id))];

    private bool IsInitialListLimited =>
        Model.AllowMultiple && Model.Options.Count > InitialOptionLimit;

    private void Toggle(CatalogueOptionModel option)
    {
        if (!_selectedOptionIds.Remove(option.Id))
        {
            if (!Model.AllowMultiple)
            {
                _selectedOptionIds.Clear();
            }

            _selectedOptionIds.Add(option.Id);
        }
    }

    private void Save() => MudDialog.Close(DialogResult.Ok(new CataloguePickerModalResult(SelectedOptions)));

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
