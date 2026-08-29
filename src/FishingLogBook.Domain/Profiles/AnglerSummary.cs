namespace FishingLogBook.Domain.Profiles;

public sealed class AnglerSummary
{
    public Guid UserId { get; init; }

    public string? DisplayName { get; init; }

    public string? PhotographObjectKey { get; init; }

    public string? HomeRegion { get; init; }
}
