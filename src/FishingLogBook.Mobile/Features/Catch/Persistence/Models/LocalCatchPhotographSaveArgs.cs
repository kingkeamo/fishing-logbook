namespace FishingLogBook.Mobile.Features.Catch.Persistence.Models;

public sealed class LocalCatchPhotographSaveArgs
{
    public required Guid Id { get; init; }

    public required int SortOrder { get; init; }

    public string? LocalFilePath { get; init; }

    public string? ContentType { get; init; }
}
