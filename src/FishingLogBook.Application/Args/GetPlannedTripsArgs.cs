namespace FishingLogBook.Application.Args;

public sealed class GetPlannedTripsArgs
{
    public Guid UserId { get; init; }

    public int? WindowDays { get; init; }
}
