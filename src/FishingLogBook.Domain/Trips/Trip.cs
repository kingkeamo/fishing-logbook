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

    public DateTimeOffset? PlannedStartOn { get; init; }

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

    public bool IsPlanned
    {
        get
        {
            return Status == TripStatusEnum.Planned;
        }
    }

    public bool IsCancelled
    {
        get
        {
            return Status == TripStatusEnum.Cancelled;
        }
    }

    public bool AcceptsContributions
    {
        get
        {
            return Status is TripStatusEnum.Active or TripStatusEnum.Completed;
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
        return Copy(
            TripStatusEnum.Completed,
            StartedOn,
            endedOn < StartedOn ? StartedOn : endedOn,
            PlannedStartOn);
    }

    public bool CanBecome(TripStatusEnum next)
    {
        return Status switch
        {
            TripStatusEnum.Planned => next is TripStatusEnum.Planned or TripStatusEnum.Active or TripStatusEnum.Cancelled,
            TripStatusEnum.Cancelled => next == TripStatusEnum.Cancelled,
            _ => next is TripStatusEnum.Active or TripStatusEnum.Completed
        };
    }

    public static bool CanBeCreatedAs(TripStatusEnum status)
    {
        return status != TripStatusEnum.Cancelled;
    }

    public Trip ReconciledWith(Trip existing)
    {
        if (IsPlanned)
        {
            return this;
        }

        if (IsCancelled)
        {
            return Copy(Status, StartedOn, EndedOn, existing.PlannedStartOn ?? PlannedStartOn);
        }

        var keepsActualStart = IsActive && existing.IsActive && existing.PlannedStartOn is not null;
        return Copy(
            Status,
            keepsActualStart ? existing.StartedOn : StartedOn,
            EndedOn,
            existing.PlannedStartOn);
    }

    public bool HasCoherentLifecycle()
    {
        if (StartedOn == default)
        {
            return false;
        }

        if (IsPlanned || IsCancelled)
        {
            return PlannedStartOn is not null && PlannedStartOn.Value != default && EndedOn is null;
        }

        if (EndedOn is not null && EndedOn.Value < StartedOn)
        {
            return false;
        }

        return !IsActive || EndedOn is null;
    }

    private Trip Copy(
        TripStatusEnum status,
        DateTimeOffset startedOn,
        DateTimeOffset? endedOn,
        DateTimeOffset? plannedStartOn)
    {
        return new Trip
        {
            Id = Id,
            OwnerUserId = OwnerUserId,
            Title = Title,
            PlaceName = PlaceName,
            Status = status,
            StartedOn = startedOn,
            PlannedStartOn = plannedStartOn,
            EndedOn = endedOn,
            Location = Location,
            CreatedOn = CreatedOn,
            UpdatedOn = UpdatedOn
        };
    }
}
