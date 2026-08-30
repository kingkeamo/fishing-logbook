using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;

public class WhenTestingSave : BaseCatchStoreTest
{
    [Fact]
    public async Task ItShouldRejectACatchWithoutPhotographs()
    {
        // Arrange
        var catchRecord = new CatchModel(Guid.NewGuid(), DateTimeOffset.UtcNow, [], UserId: OwnerUserId);

        // Act
        var act = () => Sut.SaveAsync(catchRecord, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Sut.GetAllAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectACatchWithoutAnOwner()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.UtcNow,
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])]);

        // Act
        var act = () => Sut.SaveAsync(catchRecord, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Sut.GetAllAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotLeaveACatchWhenPhotographPersistenceFails()
    {
        // Arrange
        Sut.FailPhotographWrite = true;
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.UtcNow,
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])],
            UserId: OwnerUserId);

        // Act
        var act = () => Sut.SaveAsync(catchRecord, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Sut.GetAllAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepCatchAndPhotographIdsAfterReopen()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Png, [9, 8, 7])],
            UserId: OwnerUserId,
            AnglerUserId: OwnerUserId,
            RecordedByUserId: OwnerUserId);
        await Sut.SaveAsync(catchRecord, CancellationToken.None);
        var reopened = new MemoryCatchStore(BackingCatches, BackingPhotographs);

        // Act
        var saved = await reopened.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].Photographs.Should().ContainSingle();
        saved[0].Photographs[0].Id.Should().Be(photographId);
        saved[0].Photographs[0].Bytes.Should().Equal(9, 8, 7);
        saved[0].SpeciesName.Should().BeNull();
        saved[0].UserId.Should().Be(OwnerUserId);
        saved[0].AnglerUserId.Should().Be(OwnerUserId);
        saved[0].RecordedByUserId.Should().Be(OwnerUserId);
    }

    [Fact]
    public async Task ItShouldAssignDistinctIdsToSeparatelySavedCatches()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstPhoto = Guid.NewGuid();
        var secondPhoto = Guid.NewGuid();
        await Sut.SaveAsync(
            new CatchModel(
                firstId,
                DateTimeOffset.UtcNow,
                [new CatchPhotographModel(firstPhoto, firstId, PhotographContentTypeConstants.Jpeg, [1])],
                UserId: OwnerUserId),
            CancellationToken.None);

        // Act
        await Sut.SaveAsync(
            new CatchModel(
                secondId,
                DateTimeOffset.UtcNow,
                [new CatchPhotographModel(secondPhoto, secondId, PhotographContentTypeConstants.Jpeg, [2])],
                UserId: OwnerUserId),
            CancellationToken.None);
        var saved = await Sut.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().HaveCount(2);
        saved.Select(item => item.Id).Should().BeEquivalentTo([firstId, secondId]);
        saved.SelectMany(item => item.Photographs).Select(photograph => photograph.Id)
            .Should()
            .BeEquivalentTo([firstPhoto, secondPhoto]);
        firstId.Should().NotBe(secondId);
        firstPhoto.Should().NotBe(secondPhoto);
    }

    [Fact]
    public async Task ItShouldKeepThreePhotographIdsAndBytesAfterReopen()
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
                    new CatchPhotographModel(photoA, catchId, PhotographContentTypeConstants.Jpeg, [10]),
                    new CatchPhotographModel(photoB, catchId, PhotographContentTypeConstants.Png, [20]),
                    new CatchPhotographModel(photoC, catchId, PhotographContentTypeConstants.Webp, [30])
                ],
                UserId: OwnerUserId),
            CancellationToken.None);
        var reopened = new MemoryCatchStore(BackingCatches, BackingPhotographs);

        // Act
        var saved = await reopened.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].Photographs.Should().HaveCount(3);
        saved[0].Photographs.Select(photograph => photograph.Id).Should().Equal(photoA, photoB, photoC);
        saved[0].Photographs.Should().OnlyContain(photograph => photograph.CatchId == catchId);
        saved[0].Photographs[0].Bytes.Should().Equal(10);
        saved[0].Photographs[1].Bytes.Should().Equal(20);
        saved[0].Photographs[2].Bytes.Should().Equal(30);
    }

    [Fact]
    public async Task ItShouldKeepACatchWithoutLocationAfterReopen()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await Sut.SaveAsync(
            new CatchModel(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1])],
                UserId: OwnerUserId),
            CancellationToken.None);
        var reopened = new MemoryCatchStore(BackingCatches, BackingPhotographs);

        // Act
        var saved = await reopened.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldKeepLocationAndAccuracyAfterReopen()
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
        await Sut.SaveAsync(
            new CatchModel(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1])],
                Location: location,
                UserId: OwnerUserId),
            CancellationToken.None);
        var reopened = new MemoryCatchStore(BackingCatches, BackingPhotographs);

        // Act
        var saved = await reopened.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].Location.Should().Be(location);
        saved[0].Location!.AccuracyMetres.Should().Be(12);
        saved[0].Photographs[0].Id.Should().Be(photographId);
    }

    [Fact]
    public async Task ItShouldKeepIndependentLocationsForSeparatelySavedCatches()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstLocation = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var secondLocation = new CatchLocationModel(
            53.3498,
            -6.2603,
            8,
            DateTimeOffset.Parse("2026-08-17T09:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        await Sut.SaveAsync(
            new CatchModel(
                firstId,
                DateTimeOffset.UtcNow,
                [new CatchPhotographModel(Guid.NewGuid(), firstId, PhotographContentTypeConstants.Jpeg, [1])],
                Location: firstLocation,
                UserId: OwnerUserId),
            CancellationToken.None);

        // Act
        await Sut.SaveAsync(
            new CatchModel(
                secondId,
                DateTimeOffset.UtcNow,
                [new CatchPhotographModel(Guid.NewGuid(), secondId, PhotographContentTypeConstants.Jpeg, [2])],
                Location: secondLocation,
                UserId: OwnerUserId),
            CancellationToken.None);
        var saved = await Sut.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        saved.Should().HaveCount(2);
        saved.Single(item => item.Id == firstId).Location.Should().Be(firstLocation);
        saved.Single(item => item.Id == secondId).Location.Should().Be(secondLocation);
        firstLocation.Should().NotBe(secondLocation);
    }

    [Fact]
    public async Task ItShouldKeepOptionalDetailsAfterReopenOnTheSameCatchId()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var photograph = new CatchPhotographModel(
            photographId,
            catchId,
            PhotographContentTypeConstants.Jpeg,
            [1, 2, 3],
            SyncStatus.Synchronised,
            "catch-photographs/photo");
        await Sut.SaveAsync(
            new CatchModel(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [photograph],
                UserId: OwnerUserId,
                SyncStatus: SyncStatus.Synchronised,
                MetadataSyncStatus: SyncStatus.Synchronised,
                AnglerUserId: OwnerUserId,
                RecordedByUserId: OwnerUserId),
            CancellationToken.None);
        await Sut.SaveAsync(
            new CatchModel(
                catchId,
                DateTimeOffset.Parse("2026-08-17T09:15:00Z"),
                [photograph],
                "Pike",
                UserId: OwnerUserId,
                SyncStatus: SyncStatus.WaitingToSynchronise,
                MetadataSyncStatus: SyncStatus.WaitingToSynchronise,
                AnglerUserId: OwnerUserId,
                RecordedByUserId: OwnerUserId,
                Weight: 2.5m,
                Length: 64m,
                Method: "Lure",
                BaitOrLure: "Spinner",
                Notes: "Weedline"),
            CancellationToken.None);
        var reopened = new MemoryCatchStore(BackingCatches, BackingPhotographs);

        // Act
        var saved = await reopened.GetAsync(OwnerUserId, catchId, CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(catchId);
        saved.SpeciesName.Should().Be("Pike");
        saved.Weight.Should().Be(2.5m);
        saved.Length.Should().Be(64m);
        saved.Method.Should().Be("Lure");
        saved.BaitOrLure.Should().Be("Spinner");
        saved.Notes.Should().Be("Weedline");
        saved.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-17T09:15:00Z"));
        saved.Photographs.Should().ContainSingle();
        saved.Photographs[0].Id.Should().Be(photographId);
        saved.Photographs[0].SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Photographs[0].ObjectKey.Should().Be("catch-photographs/photo");
        saved.UserId.Should().Be(OwnerUserId);
        saved.AnglerUserId.Should().Be(OwnerUserId);
        saved.RecordedByUserId.Should().Be(OwnerUserId);
        saved.MetadataSyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
    }
}
