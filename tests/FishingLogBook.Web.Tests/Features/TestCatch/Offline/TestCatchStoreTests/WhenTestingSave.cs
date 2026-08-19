using AwesomeAssertions;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Offline.Stores;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Offline.TestCatchStoreTests;

public class WhenTestingSave : BaseTestCatchStoreTest
{
    [Fact]
    public async Task ItShouldListCatchImmediately_WhenSaved()
    {
        // Arrange
        var testCatch = new TestCatchModel(
            Guid.NewGuid(),
            "Pike",
            DateTimeOffset.Parse("2026-08-14T08:00:00Z"),
            "Weed bed",
            SyncStatus.SavedLocally);

        // Act
        await Sut.SaveAsync(testCatch, CancellationToken.None);
        var saved = await Sut.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle()
            .Which.Should().Be(testCatch);
    }

    [Fact]
    public async Task ItShouldStillContainCatch_WhenNewStoreReadsSamePersistence()
    {
        // Arrange
        var testCatch = new TestCatchModel(
            Guid.NewGuid(),
            "Brown trout",
            DateTimeOffset.Parse("2026-08-14T09:15:00Z"),
            null,
            SyncStatus.SavedLocally);
        await Sut.SaveAsync(testCatch, CancellationToken.None);
        var reopened = new TestCatchStore(new MemoryTestCatchJsonStore(BackingStore));

        // Act
        var saved = await reopened.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle()
            .Which.Should().Be(testCatch);
    }

    [Fact]
    public async Task ItShouldStillContainLocation_WhenNewStoreReadsSamePersistence()
    {
        // Arrange
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            "DeviceGps",
            "Private",
            "1");
        var testCatch = new TestCatchModel(
            Guid.NewGuid(),
            "Pike",
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            null,
            SyncStatus.SavedLocally,
            Location: location);
        await Sut.SaveAsync(testCatch, CancellationToken.None);
        var reopened = new TestCatchStore(new MemoryTestCatchJsonStore(BackingStore));

        // Act
        var saved = await reopened.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Location.Should().Be(location);
    }
}
