namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportTripCatchModel(Guid TripId, Guid CatchId, string Title);