using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
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
    }
}
