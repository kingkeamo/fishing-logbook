using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripListItemModel(
    Guid Id,
    string Status,
    DateTimeOffset StartedOn,
    DateTimeOffset? EndedOn = null)
{
    public string? Title { get; init; }

    public string? PlaceName { get; init; }

    public int CatchCount { get; init; }

    public int PhotographCount { get; init; }

    public int NoteCount { get; init; }

    public bool IsShared { get; init; }

    public bool CanUpdate { get; init; } = true;

    public DateTimeOffset? PlannedStartOn { get; init; }

    public bool IsPlanned
    {
        get
        {
            return Status == TripConstants.Planned;
        }
    }

    public bool IsCancelled
    {
        get
        {
            return Status == TripConstants.Cancelled;
        }
    }

    public bool HasPlannedLifecycle
    {
        get
        {
            return IsPlanned || IsCancelled;
        }
    }

    public bool IsActive
    {
        get
        {
            return Status == TripConstants.Active;
        }
    }
}
