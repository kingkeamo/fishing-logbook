namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripPhotographRowModel(Guid Id, string? Source, bool CanRemove, string RemoveLabel)
{
    public bool HasSource => !string.IsNullOrWhiteSpace(Source);
}
