namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripDisplayModel(
    string? StartedDate,
    string? StartedTime,
    string? EndedTime,
    TimeSpan? Elapsed);
