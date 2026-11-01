namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripListItemModel(
    Guid Id,
    string DateLabel,
    string? Title,
    string? Place,
    bool IsShared,
    bool IsActive,
    bool IsPending,
    string CatchesText,
    string PhotographsText,
    string NotesText,
    string ActionText,
    string StatusText,
    string SharedText,
    string PendingText)
{
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

    public bool HasPlace => !string.IsNullOrWhiteSpace(Place);
}
