using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripInvitationNotFoundError : Error
{
    public TripInvitationNotFoundError()
        : base("The trip invitation was not found.")
    {
    }
}
