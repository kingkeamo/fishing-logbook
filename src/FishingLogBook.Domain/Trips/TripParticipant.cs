using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Domain.Trips;

public sealed class TripParticipant
{
    public Guid Id { get; init; }

    public Guid TripId { get; init; }

    public Guid UserId { get; init; }

    public TripParticipantStatusEnum Status { get; init; }

    public Guid InvitedByUserId { get; init; }

    public DateTimeOffset InvitedOn { get; init; }

    public DateTimeOffset? RespondedOn { get; init; }

    public DateTimeOffset? RemovedOn { get; init; }

    public bool IsPending
    {
        get
        {
            return Status == TripParticipantStatusEnum.Pending && RemovedOn is null;
        }
    }

    public bool IsContributing
    {
        get
        {
            return Status == TripParticipantStatusEnum.Accepted && RemovedOn is null;
        }
    }

    public TripParticipant RespondedAt(TripParticipantStatusEnum status, DateTimeOffset respondedOn)
    {
        return new TripParticipant
        {
            Id = Id,
            TripId = TripId,
            UserId = UserId,
            Status = status,
            InvitedByUserId = InvitedByUserId,
            InvitedOn = InvitedOn,
            RespondedOn = respondedOn < InvitedOn ? InvitedOn : respondedOn,
            RemovedOn = null
        };
    }

    public TripParticipant RemovedAt(DateTimeOffset removedOn)
    {
        return new TripParticipant
        {
            Id = Id,
            TripId = TripId,
            UserId = UserId,
            Status = Status,
            InvitedByUserId = InvitedByUserId,
            InvitedOn = InvitedOn,
            RespondedOn = RespondedOn,
            RemovedOn = removedOn < InvitedOn ? InvitedOn : removedOn
        };
    }

    public TripParticipant ReinvitedAt(Guid invitedByUserId, DateTimeOffset invitedOn)
    {
        return new TripParticipant
        {
            Id = Id,
            TripId = TripId,
            UserId = UserId,
            Status = TripParticipantStatusEnum.Pending,
            InvitedByUserId = invitedByUserId,
            InvitedOn = invitedOn,
            RespondedOn = null,
            RemovedOn = null
        };
    }
}
