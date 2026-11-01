namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripInvitationItemModel(Guid TripId, string FromText, string Summary, string AcceptText, string DeclineText);
