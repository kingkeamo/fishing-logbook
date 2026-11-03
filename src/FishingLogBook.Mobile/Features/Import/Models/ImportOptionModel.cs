namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportOptionModel(
    Guid Id,
    string Code,
    string Name,
    bool IsSelected = false,
    Guid? CatchId = null);
