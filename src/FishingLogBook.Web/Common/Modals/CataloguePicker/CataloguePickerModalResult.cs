namespace FishingLogBook.Web.Common.Modals;

public sealed record CataloguePickerModalResult(IReadOnlyList<CatalogueOptionModel> Options)
{
    public CataloguePickerModalResult(CatalogueOptionModel option)
        : this([option])
    {
    }
}
