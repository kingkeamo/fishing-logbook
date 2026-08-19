namespace FishingLogBook.Web.Common.Modals;

public sealed record CataloguePickerModalModel(
    string Title,
    IReadOnlyList<CatalogueOptionModel> Options);
