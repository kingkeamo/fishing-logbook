namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportCatalogueSelectionModel(Guid Id, string Code, string Name)
{
    public bool IsValid
    {
        get
        {
            return Id != Guid.Empty
                && !string.IsNullOrWhiteSpace(Code)
                && !string.IsNullOrWhiteSpace(Name);
        }
    }
}
