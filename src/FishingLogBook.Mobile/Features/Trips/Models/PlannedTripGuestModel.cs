namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record PlannedTripGuestModel(Guid UserId, string? DisplayName, bool IsPending);
