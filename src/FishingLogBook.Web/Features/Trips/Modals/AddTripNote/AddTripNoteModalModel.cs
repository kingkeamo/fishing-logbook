namespace FishingLogBook.Web.Features.Trips.Modals.AddTripNote;

public sealed record AddTripNoteModalModel(
    Guid TripId,
    Guid OwnerUserId,
    DateTimeOffset TripStartedOn);
