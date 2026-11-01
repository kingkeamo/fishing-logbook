namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripAnglerOptionModel(Guid UserId, string? DisplayName, bool IsSelf);
