using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;

namespace FishingLogBook.Application.Tests.Trips.Entities.TripTests;

public abstract class BaseTripTest
{
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly DateTimeOffset PlannedStartOn = DateTimeOffset.Parse("2026-09-12T06:00:00Z");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-09-12T06:20:00Z");

    protected static Trip NewTrip(
        TripStatusEnum status,
        DateTimeOffset? startedOn = null,
        DateTimeOffset? plannedStartOn = null,
        DateTimeOffset? endedOn = null,
        string? title = null)
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = OwnerId,
            Title = title,
            Status = status,
            StartedOn = startedOn ?? StartedOn,
            PlannedStartOn = plannedStartOn,
            EndedOn = endedOn
        };
    }
}
