namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportCatchPhotoModel(
    Guid CatchProposalId,
    ImportSelectedPhotoModel Photo,
    bool IsSelected)
{
    public string Label => string.IsNullOrWhiteSpace(Photo.FileName)
        ? (Photo.SelectionIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Photo.FileName;
}
