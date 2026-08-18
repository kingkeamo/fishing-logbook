using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Common.Modals;

public partial class CataloguePickerModal : ComponentBase
{
    private string? _search;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public CataloguePickerModalModel Model { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<CatalogueOptionModel> FilteredOptions
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return Model.Options;
            }

            var term = _search.Trim();
            return [.. Model.Options.Where(option =>
                option.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))];
        }
    }

    private void Select(CatalogueOptionModel option)
    {
        MudDialog.Close(DialogResult.Ok(new CataloguePickerModalResult(option)));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
