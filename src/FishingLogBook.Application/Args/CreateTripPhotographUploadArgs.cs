using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Args;

public sealed class CreateTripPhotographUploadArgs
{
    public Guid TripId { get; init; }

    public PhotographUploadRequestDto Request { get; init; } = new(Guid.Empty, string.Empty);
}
