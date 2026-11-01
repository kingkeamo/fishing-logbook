namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ExistingTripItemModel(Guid TripId, string Title, string WhenText, string ActionText);
