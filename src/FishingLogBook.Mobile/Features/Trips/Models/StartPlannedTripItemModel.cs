namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record StartPlannedTripItemModel(
    Guid TripId,
    string NameText,
    string WhenText,
    string OrganiserText,
    string GuestsText,
    string ActionText)
{
    public bool HasGuests => !string.IsNullOrWhiteSpace(GuestsText);
}
