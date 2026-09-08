using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Locations.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportLocationLookupServiceTests;

public class BaseImportLocationLookupServiceTest
{
    protected static (ImportLocationLookupService Service, ILocationLookupClient Client) CreateService()
    {
        var client = Substitute.For<ILocationLookupClient>();
        return (new ImportLocationLookupService(client), client);
    }

    protected static ImportSelectedPhotoModel Photo(
        int index,
        double? latitude,
        double? longitude)
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
        return photo;
    }
}
