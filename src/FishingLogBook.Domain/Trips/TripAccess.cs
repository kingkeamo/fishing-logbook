using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Domain.Trips;

public sealed class TripAccess
{
    private TripAccess(Trip trip, Guid userId, TripAccessRoleEnum role)
    {
        Trip = trip;
        UserId = userId;
        Role = role;
    }

    public Trip Trip { get; }

    public Guid UserId { get; }

    public TripAccessRoleEnum Role { get; }

    public Guid TripId
    {
        get
        {
            return Trip.Id;
        }
    }

    public bool CanView
    {
        get
        {
            return Role != TripAccessRoleEnum.None;
        }
    }

    public bool CanContribute
    {
        get
        {
            return Role is TripAccessRoleEnum.Owner or TripAccessRoleEnum.Participant;
        }
    }

    public bool CanManageTrip
    {
        get
        {
            return Role == TripAccessRoleEnum.Owner;
        }
    }

    public static TripAccess Resolve(Trip trip, Guid userId, TripParticipant? participant)
    {
        if (trip.OwnerUserId == userId)
        {
            return new TripAccess(trip, userId, TripAccessRoleEnum.Owner);
        }

        if (participant is not null
            && participant.TripId == trip.Id
            && participant.UserId == userId
            && participant.IsContributing)
        {
            return new TripAccess(trip, userId, TripAccessRoleEnum.Participant);
        }

        return new TripAccess(trip, userId, TripAccessRoleEnum.None);
    }
}
