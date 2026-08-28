using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Modals.AddTripNote;

public sealed record AddTripNoteModalModel(
    Guid TripId,
    Guid OwnerUserId,
    DateTimeOffset TripStartedOn,
    DateTimeOffset? TripEndedOn = null,
    TripStorageEnum Storage = TripStorageEnum.LocalFirst,
    TripNoteModel? ExistingNote = null);
