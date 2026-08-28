namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripCatchScopeModel(
    Guid TripId,
    Guid OwnerUserId,
    DateTimeOffset StartedOn,
    DateTimeOffset? EndedOn = null);
