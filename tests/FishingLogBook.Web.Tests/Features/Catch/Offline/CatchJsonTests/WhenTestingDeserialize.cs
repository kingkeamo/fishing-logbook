using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.CatchJsonTests;

public class WhenTestingDeserialize : BaseCatchJsonTest
{
    [Fact]
    public void ItShouldOrderPhotographsByMetadataRatherThanInputOrder()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photoA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var photoB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var photoC = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var json = CatchJson.SerializeMetadata(
            new CatchModel(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [
                    new CatchPhotographModel(photoA, catchId, PhotographContentTypeConstants.Jpeg, [1]),
                    new CatchPhotographModel(photoB, catchId, PhotographContentTypeConstants.Png, [2]),
                    new CatchPhotographModel(photoC, catchId, PhotographContentTypeConstants.Webp, [3])
                ]));
        var unordered = new[]
        {
            new CatchPhotographModel(photoC, catchId, PhotographContentTypeConstants.Webp, [3]),
            new CatchPhotographModel(photoA, catchId, PhotographContentTypeConstants.Jpeg, [1]),
            new CatchPhotographModel(photoB, catchId, PhotographContentTypeConstants.Png, [2])
        };

        // Act
        var restored = CatchJson.Deserialize(json, unordered);

        // Assert
        restored.Id.Should().Be(catchId);
        restored.Photographs.Select(photograph => photograph.Id).Should().Equal(photoA, photoB, photoC);
        restored.Photographs.Select(photograph => photograph.Bytes![0]).Should().Equal(1, 2, 3);
        restored.Photographs.Select(photograph => photograph.ContentType)
            .Should()
            .Equal(
                PhotographContentTypeConstants.Jpeg,
                PhotographContentTypeConstants.Png,
                PhotographContentTypeConstants.Webp);
        restored.Photographs.Should().OnlyContain(photograph => photograph.CatchId == catchId);
        restored.UserId.Should().Be(Guid.Empty);
        restored.AnglerUserId.Should().Be(Guid.Empty);
        restored.RecordedByUserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ItShouldReadLegacyMetadataWithoutALocationProperty()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var json = """
            {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","caughtOn":"2026-08-17T08:00:00+00:00","speciesName":null,"photographs":[{"id":"11111111-1111-1111-1111-111111111111","catchId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","contentType":"image/jpeg"}]}
            """;
        var photographs = new[]
        {
            new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1])
        };

        // Act
        var restored = CatchJson.Deserialize(json, photographs);

        // Assert
        restored.Id.Should().Be(catchId);
        restored.Location.Should().BeNull();
        restored.UserId.Should().Be(Guid.Empty);
        restored.AnglerUserId.Should().Be(Guid.Empty);
        restored.RecordedByUserId.Should().Be(Guid.Empty);
        restored.Photographs.Should().ContainSingle(photograph => photograph.Id == photographId);
    }

    [Fact]
    public void ItShouldRoundTripTheOwnerUserId()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var photographId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1])],
            UserId: userId);

        // Act
        var json = CatchJson.SerializeMetadata(catchRecord);
        var restored = CatchJson.Deserialize(json, catchRecord.Photographs);

        // Assert
        json.Should().Contain("\"userId\":\"11111111-1111-1111-1111-111111111111\"");
        restored.UserId.Should().Be(userId);
        restored.AnglerUserId.Should().Be(userId);
        restored.RecordedByUserId.Should().Be(userId);
        restored.Id.Should().Be(catchId);
    }

    [Fact]
    public void ItShouldRoundTripProvenanceUserIds()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var photographId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1])],
            UserId: userId,
            AnglerUserId: userId,
            RecordedByUserId: userId);

        // Act
        var json = CatchJson.SerializeMetadata(catchRecord);
        var restored = CatchJson.Deserialize(json, catchRecord.Photographs);

        // Assert
        json.Should().Contain("\"anglerUserId\":\"11111111-1111-1111-1111-111111111111\"");
        json.Should().Contain("\"recordedByUserId\":\"11111111-1111-1111-1111-111111111111\"");
        restored.UserId.Should().Be(userId);
        restored.AnglerUserId.Should().Be(userId);
        restored.RecordedByUserId.Should().Be(userId);
    }

    [Fact]
    public void ItShouldFallBackToTheOwnerWhenLegacyMetadataOmitsProvenance()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var photographId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var json = """
            {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","caughtOn":"2026-08-17T08:00:00+00:00","speciesName":null,"photographs":[{"id":"22222222-2222-2222-2222-222222222222","catchId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","contentType":"image/jpeg"}],"userId":"11111111-1111-1111-1111-111111111111"}
            """;
        var photographs = new[]
        {
            new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1])
        };

        // Act
        var restored = CatchJson.Deserialize(json, photographs);

        // Assert
        restored.UserId.Should().Be(userId);
        restored.AnglerUserId.Should().Be(userId);
        restored.RecordedByUserId.Should().Be(userId);
    }

    [Fact]
    public void ItShouldFallBackToTheOwnerWhenBrowserStorageReturnsPre18Json()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var json = """
            {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","userId":"11111111-1111-1111-1111-111111111111","caughtOn":"2026-08-17T08:00:00+00:00","photographs":[{"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","catchId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","contentType":"image/jpeg"}]}
            """;
        var photographs = new[]
        {
            new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [4, 5, 6])
        };

        // Act
        var restored = CatchJson.Deserialize(json, photographs);

        // Assert
        restored.UserId.Should().Be(userId);
        restored.AnglerUserId.Should().Be(userId);
        restored.RecordedByUserId.Should().Be(userId);
        restored.Photographs.Should().ContainSingle(photograph => photograph.Id == photographId);
    }

    [Fact]
    public void ItShouldKeepProvenanceWhenBrowserStorageReturnsOwnedJson()
    {
        // Arrange
        var catchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var photographId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var json = """
            {"id":"11111111-1111-1111-1111-111111111111","userId":"11111111-1111-1111-1111-111111111111","anglerUserId":"11111111-1111-1111-1111-111111111111","recordedByUserId":"11111111-1111-1111-1111-111111111111","caughtOn":"2026-08-17T08:00:00+00:00","photographs":[{"id":"22222222-2222-2222-2222-222222222222","catchId":"11111111-1111-1111-1111-111111111111","contentType":"image/jpeg"}]}
            """;
        var photographs = new[]
        {
            new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])
        };

        // Act
        var restored = CatchJson.Deserialize(json, photographs);

        // Assert
        restored.UserId.Should().Be(userId);
        restored.AnglerUserId.Should().Be(userId);
        restored.RecordedByUserId.Should().Be(userId);
        restored.AnglerUserId.Should().Be(restored.UserId);
        restored.RecordedByUserId.Should().Be(restored.UserId);
    }

    [Fact]
    public void ItShouldRoundTripANullLocation()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1])]);

        // Act
        var restored = CatchJson.Deserialize(
            CatchJson.SerializeMetadata(catchRecord),
            catchRecord.Photographs);

        // Assert
        restored.Location.Should().BeNull();
        restored.Id.Should().Be(catchId);
    }

    [Fact]
    public void ItShouldRoundTripCapturedLocationAndAccuracy()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1])],
            Location: location);

        // Act
        var json = CatchJson.SerializeMetadata(catchRecord);
        var restored = CatchJson.Deserialize(json, catchRecord.Photographs);

        // Assert
        json.Should().Contain("\"latitude\":53.2707");
        json.Should().Contain("\"accuracyMetres\":12");
        restored.Location.Should().Be(location);
        restored.Location!.Visibility.Should().Be(LocationDefaults.Private);
        restored.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        restored.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }
}
