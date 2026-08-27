using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Domain.Trips;

public sealed class Trip
{
    public Guid Id { get; init; }

    public Guid OwnerUserId { get; init; }

    public string? Title { get; init; }

    public string? PlaceName { get; init; }

    public TripStatusEnum Status { get; init; }

    public DateTimeOffset StartedOn { get; init; }

    public DateTimeOffset? EndedOn { get; init; }

    public TripLocation? Location { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset UpdatedOn { get; init; }

    public bool IsActive
    {
        get
        {
            return Status == TripStatusEnum.Active;
        }
    }

    public bool OutranksActive(Trip other)
    {
        if (StartedOn != other.StartedOn)
        {
            return StartedOn > other.StartedOn;
        }

        return Id.CompareTo(other.Id) > 0;
    }

    public Trip CompletedAt(DateTimeOffset endedOn)
    {
        return new Trip
        {
            Id = Id,
            OwnerUserId = OwnerUserId,
            Title = Title,
            PlaceName = PlaceName,
            Status = TripStatusEnum.Completed,
            StartedOn = StartedOn,
            EndedOn = endedOn < StartedOn ? StartedOn : endedOn,
            Location = Location,
            CreatedOn = CreatedOn,
            UpdatedOn = UpdatedOn
        };
    }

    public bool HasCoherentLifecycle()
    {
        if (StartedOn == default)
        {
            return false;
        }

        if (EndedOn is not null && EndedOn.Value < StartedOn)
        {
            return false;
        }

        return !IsActive || EndedOn is null;
    }
}
