using FishingLogBook.Shared.Photographs.Models;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Models;

public sealed class LocalCatchPhotographSaveArgs
{
    public required Guid Id { get; init; }

    public required int SortOrder { get; init; }

    public string? LocalFilePath { get; init; }

    public string? ContentType { get; init; }

    public PhotographMetadataModel Metadata { get; init; } = PhotographMetadataModel.Empty;
}
