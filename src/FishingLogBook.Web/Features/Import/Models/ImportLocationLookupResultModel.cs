namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportLocationLookupResultModel(
    string DisplayName,
    string? Locality = null,
    string? Region = null,
    string? Country = null);
