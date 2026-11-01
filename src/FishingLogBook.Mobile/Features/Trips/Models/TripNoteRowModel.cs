namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripNoteRowModel(Guid Id, string TimeText, string Text, bool CanChange);
