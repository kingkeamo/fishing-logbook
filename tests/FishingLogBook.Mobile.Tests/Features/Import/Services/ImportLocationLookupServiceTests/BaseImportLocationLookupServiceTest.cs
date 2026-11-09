using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Locations.Clients;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportLocationLookupServiceTests;

public class BaseImportLocationLookupServiceTest
{
    protected readonly ILocationLookupClient MockClient = Substitute.For<ILocationLookupClient>();
    protected readonly ImportLocationLookupService Sut;

    protected BaseImportLocationLookupServiceTest()
    {
        Sut = new ImportLocationLookupService(MockClient);
    }

    protected static ImportSelectedPhotoModel Photo(
        int index,
        double? latitude,
        double? longitude,
        ImportLocationLookupStatusEnum status = ImportLocationLookupStatusEnum.NotRequested)
    {
        var photo = new ImportSelectedPhotoModel(
            Guid.NewGuid(),
            index,
            "image/jpeg",
            1024,
            $"token-{index}",
            thumbnailUrl: $"blob:photo-{index}");
        photo.SetPreparation(
            ImportPhotoPreparationStatusEnum.Ready,
            $"token-{index}",
            $"blob:photo-{index}");
        photo.SetMetadata(
            ImportMetadataStatusEnum.Available,
            ImportTimestampModel.FromExplicitInstant(
                DateTimeOffset.Parse("2026-08-27T14:42:00+00:00"),
                ImportTimestampSourceEnum.ExifOriginal),
            new ImportLocationModel(latitude, longitude, latitude.HasValue));
        if (status != ImportLocationLookupStatusEnum.NotRequested)
        {
            photo.SetLocation(photo.Location.WithLookup(status));
        }

        return photo;
    }
}
