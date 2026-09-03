using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingSelectedPhoto : BaseImportModelTest
{
    [Fact]
    public void ItShouldRepresentMetadataLookupAndDuplicatePlaceholdersWithoutPhotoBytes()
    {
        // Arrange
        var photo = Photo();
        var timestamp = ImportTimestampModel.FromLocalWallClock(
            new DateTime(2025, 6, 14, 9, 30, 0),
            ImportTimestampSourceEnum.ExifOriginal);
        var location = new ImportLocationModel(53.3498, -6.2603, true)
            .WithLookup(ImportLocationLookupStatusEnum.Pending);

        // Act
        photo.SetMetadata(ImportMetadataStatusEnum.Available, timestamp, location);
        photo.SetDuplicateState(ImportDuplicateStatusEnum.Warning, "fingerprint-placeholder");

        // Assert
        photo.MetadataStatus.Should().Be(ImportMetadataStatusEnum.Available);
        photo.Timestamp.Should().Be(timestamp);
        photo.Location.Should().Be(location);
        photo.DuplicateStatus.Should().Be(ImportDuplicateStatusEnum.Warning);
        photo.Fingerprint.Should().Be("fingerprint-placeholder");
        photo.GetType().GetProperties().Select(property => property.PropertyType)
            .Should().NotContain(typeof(byte[]));
    }
}
