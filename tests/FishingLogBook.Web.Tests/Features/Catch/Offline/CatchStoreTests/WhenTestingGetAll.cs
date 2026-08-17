using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.CatchStoreTests;

public class WhenTestingGetAll : BaseCatchStoreTest
{
    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenNothingIsSaved()
    {
        // Arrange
        // Act
        var saved = await Sut.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnSavedCatchesWithPhotographBytes()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await Sut.SaveAsync(
            new CatchModel(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [4, 5, 6])]),
            CancellationToken.None);

        // Act
        var saved = await Sut.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].Photographs[0].Id.Should().Be(photographId);
        saved[0].Photographs[0].Bytes.Should().Equal(4, 5, 6);
    }

    [Fact]
    public async Task ItShouldReturnThreePhotographsInCaptureOrderAfterReopen()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photoA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var photoB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var photoC = Guid.Parse("00000000-0000-0000-0000-000000000003");
        await Sut.SaveAsync(
            new CatchModel(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [
                    new CatchPhotographModel(photoA, catchId, PhotographContentTypeConstants.Jpeg, [1, 1, 1]),
                    new CatchPhotographModel(photoB, catchId, PhotographContentTypeConstants.Png, [2, 2, 2]),
                    new CatchPhotographModel(photoC, catchId, PhotographContentTypeConstants.Webp, [3, 3, 3])
                ]),
            CancellationToken.None);
        var reopened = new MemoryCatchStore(BackingCatches, BackingPhotographs);

        // Act
        var saved = await reopened.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].Photographs.Select(photograph => photograph.Id).Should().Equal(photoA, photoB, photoC);
        saved[0].Photographs.Select(photograph => photograph.CatchId).Should().OnlyContain(id => id == catchId);
        saved[0].Photographs[0].Bytes.Should().Equal(1, 1, 1);
        saved[0].Photographs[1].Bytes.Should().Equal(2, 2, 2);
        saved[0].Photographs[2].Bytes.Should().Equal(3, 3, 3);
        saved[0].Photographs.Select(photograph => photograph.ContentType)
            .Should()
            .Equal(
                PhotographContentTypeConstants.Jpeg,
                PhotographContentTypeConstants.Png,
                PhotographContentTypeConstants.Webp);
    }
}
