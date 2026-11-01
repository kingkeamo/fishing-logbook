namespace FishingLogBook.Application.Args;

public sealed class FindPlannedTripsArgs
{
    public Guid UserId { get; init; }

    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }
}
