using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Args;

public sealed class UpsertTripArgs
{
    public Guid UserId { get; init; }

    public TripDto Trip { get; init; } = new(Guid.Empty, string.Empty, default);
}
