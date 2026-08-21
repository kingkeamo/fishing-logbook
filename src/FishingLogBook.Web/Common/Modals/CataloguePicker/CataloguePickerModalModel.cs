namespace FishingLogBook.Web.Common.Modals;

public sealed record CataloguePickerModalModel(
    string Title,
    IReadOnlyList<CatalogueOptionModel> Options,
    IReadOnlySet<Guid>? SelectedOptionIds = null,
    bool AllowMultiple = false,
    string? ItemPluralName = null);
