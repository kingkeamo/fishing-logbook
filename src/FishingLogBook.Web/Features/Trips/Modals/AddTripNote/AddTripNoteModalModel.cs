using FishingLogBook.Web.Features.Trips.Enums;

namespace FishingLogBook.Web.Features.Trips.Modals.AddTripNote;

public sealed record AddTripNoteModalModel(
    Guid TripId,
    Guid OwnerUserId,
    DateTimeOffset TripStartedOn,
    DateTimeOffset? TripEndedOn = null,
    TripNoteStorageEnum Storage = TripNoteStorageEnum.LocalFirst);
