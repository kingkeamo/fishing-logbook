using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;

public class WhenTestingGetAll : BaseCatchStoreTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyOwner()
    {
        // Arrange
        // Act
        var act = () => Sut.GetAllAsync(Guid.Empty, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenNothingIsSaved()
    {
        // Arrange
        // Act
        var saved = await Sut.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherUsersCatchOrPhotograph()
    {
        // Arrange
        var ownerCatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherCatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var ownerPhotoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var otherPhotoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var otherLocation = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        await Sut.SaveAsync(
            new CatchModel(
                ownerCatchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographModel(ownerPhotoId, ownerCatchId, PhotographContentTypeConstants.Jpeg, [4, 5, 6])],
                CaughtByUserId: OwnerUserId),
            CancellationToken.None);
        await Sut.SaveAsync(
            new CatchModel(
                otherCatchId,
                DateTimeOffset.Parse("2026-08-17T09:00:00Z"),
                [new CatchPhotographModel(otherPhotoId, otherCatchId, PhotographContentTypeConstants.Jpeg, [7, 8, 9])],
                Location: otherLocation,
                CaughtByUserId: OtherUserId),
            CancellationToken.None);

        // Act
        var ownerView = await Sut.GetAllAsync(OwnerUserId, CancellationToken.None);
        var otherView = await Sut.GetAllAsync(OtherUserId, CancellationToken.None);

        // Assert
        ownerView.Should().ContainSingle();
        ownerView[0].Id.Should().Be(ownerCatchId);
        ownerView[0].CaughtByUserId.Should().Be(OwnerUserId);
        ownerView[0].Photographs[0].Id.Should().Be(ownerPhotoId);
        ownerView[0].Photographs[0].Bytes.Should().Equal(4, 5, 6);
        ownerView.Should().NotContain(catchRecord => catchRecord.Id == otherCatchId);
        otherView.Should().ContainSingle();
        otherView[0].Id.Should().Be(otherCatchId);
        otherView[0].CaughtByUserId.Should().Be(OtherUserId);
        otherView[0].Location.Should().Be(otherLocation);
        otherView[0].Photographs[0].Bytes.Should().Equal(7, 8, 9);
        otherView.Should().NotContain(catchRecord => catchRecord.Id == ownerCatchId);
    }

    [Fact]
    public async Task ItShouldNotExposeOrAdoptAnUnscopedCatchWhenAnotherUserSignsInFirst()
    {
        // Arrange
        var unscopedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var unscopedPhoto = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        BackingCatches[unscopedId] = new CatchModel(
            unscopedId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(unscopedPhoto, unscopedId, PhotographContentTypeConstants.Jpeg, null)],
            Location: location);
        BackingPhotographs[unscopedPhoto] = [4, 5, 6];

        // Act
        var firstSignerView = await Sut.GetAllAsync(OtherUserId, CancellationToken.None);
        var originalOwnerView = await Sut.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        firstSignerView.Should().BeEmpty();
        originalOwnerView.Should().BeEmpty();
        BackingCatches[unscopedId].CaughtByUserId.Should().Be(Guid.Empty);
        BackingCatches[unscopedId].Location.Should().Be(location);
        BackingPhotographs[unscopedPhoto].Should().Equal(4, 5, 6);
        firstSignerView.Should().NotContain(catchRecord => catchRecord.Id == unscopedId);
        firstSignerView
            .SelectMany(catchRecord => catchRecord.Photographs)
            .Should()
            .NotContain(photograph => photograph.Id == unscopedPhoto);
    }

    [Fact]
    public async Task ItShouldNotExposeUnscopedCatchesAlongsideOwnedRecords()
    {
        // Arrange
        var unscopedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var unscopedPhoto = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var ownerCatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        BackingCatches[unscopedId] = new CatchModel(
            unscopedId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(unscopedPhoto, unscopedId, PhotographContentTypeConstants.Jpeg, null)]);
        BackingPhotographs[unscopedPhoto] = [1];
        await Sut.SaveAsync(
            new CatchModel(
                ownerCatchId,
                DateTimeOffset.Parse("2026-08-17T09:00:00Z"),
                [new CatchPhotographModel(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    ownerCatchId,
                    PhotographContentTypeConstants.Jpeg,
                    [2])],
                CaughtByUserId: OwnerUserId),
            CancellationToken.None);

        // Act
        var otherView = await Sut.GetAllAsync(OtherUserId, CancellationToken.None);
        var ownerView = await Sut.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        otherView.Should().BeEmpty();
        ownerView.Should().ContainSingle();
        ownerView[0].Id.Should().Be(ownerCatchId);
        ownerView[0].CaughtByUserId.Should().Be(OwnerUserId);
        ownerView.Should().NotContain(catchRecord => catchRecord.Id == unscopedId);
        BackingCatches[unscopedId].CaughtByUserId.Should().Be(Guid.Empty);
        BackingPhotographs[unscopedPhoto].Should().Equal(1);
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
                [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [4, 5, 6])],
                CaughtByUserId: OwnerUserId),
            CancellationToken.None);

        // Act
        var saved = await Sut.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].CaughtByUserId.Should().Be(OwnerUserId);
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
                ],
                CaughtByUserId: OwnerUserId),
            CancellationToken.None);
        var reopened = new MemoryCatchStore(BackingCatches, BackingPhotographs);

        // Act
        var saved = await reopened.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].CaughtByUserId.Should().Be(OwnerUserId);
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
