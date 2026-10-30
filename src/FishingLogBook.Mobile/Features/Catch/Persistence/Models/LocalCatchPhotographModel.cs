namespace FishingLogBook.Mobile.Features.Catch.Persistence.Models;

public sealed class LocalCatchPhotographModel
{
    public required Guid Id { get; init; }

    public required Guid CatchId { get; init; }

    public required Guid UserId { get; init; }

    public required int SortOrder { get; init; }

    public string? LocalFilePath { get; init; }

    public string? ContentType { get; init; }

    public required DateTimeOffset CreatedOn { get; init; }
}
