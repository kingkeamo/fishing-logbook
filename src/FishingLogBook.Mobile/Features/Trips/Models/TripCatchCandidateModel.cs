namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripCatchCandidateModel(Guid Id, DateTimeOffset CaughtOn, string? SpeciesName, decimal? Weight, decimal? Length);
