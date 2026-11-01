namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record PlannedTripOptionModel(
    Guid TripId,
    DateTimeOffset PlannedStartOn,
    Guid OrganiserUserId,
    string? OrganiserName,
    bool IsOrganiser)
{
    public string? Title { get; init; }

    public string? PlaceName { get; init; }

    public IReadOnlyList<PlannedTripGuestModel> Guests { get; init; } = [];
}
