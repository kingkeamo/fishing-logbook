namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripNoteDraftModel(
    Guid TripId,
    Guid OwnerUserId,
    string Text,
    DateTimeOffset RecordedOn);
