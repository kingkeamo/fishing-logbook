namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripParticipantItemModel(Guid UserId, string Name, string Status, bool CanRemove);
