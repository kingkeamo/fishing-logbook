namespace FishingLogBook.Mobile.Features.Photographs.Models;

public sealed class PhotographEditorItemModel
{
    public required Guid Id { get; init; }

    public required int SortOrder { get; init; }

    public required string PreviewPath { get; init; }

    public required string RelativeFilePath { get; init; }

    public required string ContentType { get; init; }
}
