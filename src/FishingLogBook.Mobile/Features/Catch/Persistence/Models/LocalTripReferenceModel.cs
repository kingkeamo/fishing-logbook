namespace FishingLogBook.Mobile.Features.Catch.Persistence.Models;

public sealed class LocalTripReferenceModel
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public string? Title { get; init; }

    public required DateTimeOffset UpdatedOn { get; init; }
}
