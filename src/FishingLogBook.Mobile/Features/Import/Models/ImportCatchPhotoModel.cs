namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportCatchPhotoModel(
    Guid CatchProposalId,
    ImportSelectedPhotoModel Photo,
    bool IsSelected,
    string Label)
{
    public string? ThumbnailUrl => Photo.ThumbnailUrl;

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(Photo.ThumbnailUrl);
}
