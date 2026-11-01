namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportPhotoRowModel(
    ImportSelectedPhotoModel Photo,
    string StatusText,
    string DuplicateText,
    bool RequiresDuplicateDecision,
    bool HasWarning);
