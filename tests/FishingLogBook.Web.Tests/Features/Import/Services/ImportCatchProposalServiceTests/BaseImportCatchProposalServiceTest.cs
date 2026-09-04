using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportCatchProposalServiceTests;

public class BaseImportCatchProposalServiceTest
{
    protected static readonly ImportCatchProposalService Sut = new();
    protected static readonly DateTimeOffset CapturedOn = DateTimeOffset.Parse("2025-06-14T09:00:00Z");

    protected static ImportBatchModel Batch(params ImportSelectedPhotoModel[] photos)
    {
        var batch = new ImportBatchModel(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new ImportCatalogueSelectionModel(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Fly",
                "Fly"),
            new ImportCatalogueSelectionModel(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "BrownTrout",
                "Brown Trout"));
        foreach (var photo in photos)
        {
            batch.AddPhoto(photo);
        }

        return batch;
    }

    protected static ImportSelectedPhotoModel Photo(
        int index,
        ImportTimestampModel timestamp,
        double? latitude = null,
        double? longitude = null,
        bool ready = true)
    {
        var photo = new ImportSelectedPhotoModel(
            Guid.Parse($"44444444-4444-4444-4444-{index + 1:D12}"),
            index,
            "image/jpeg",
            1024,
            ready ? $"blob-{index}" : null,
            $"photo-{index}.jpg",
            ready ? $"blob:thumbnail-{index}" : null);
        photo.SetMetadata(
            ImportMetadataStatusEnum.Available,
            timestamp,
            new ImportLocationModel(latitude, longitude, latitude.HasValue));
        if (ready)
        {
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, $"blob-{index}", $"blob:thumbnail-{index}");
        }

        return photo;
    }

    protected static ImportTimestampModel ExplicitAt(TimeSpan offset)
    {
        return ImportTimestampModel.FromExplicitInstant(
            CapturedOn.Add(offset),
            ImportTimestampSourceEnum.ExifOriginal);
    }
}
