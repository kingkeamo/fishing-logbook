namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportTripParticipantModel(Guid TripId, Guid UserId, string Name);
