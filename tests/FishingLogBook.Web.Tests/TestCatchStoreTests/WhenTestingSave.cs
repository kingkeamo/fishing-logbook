using AwesomeAssertions;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;

namespace FishingLogBook.Web.Tests.TestCatchStoreTests;

public class WhenTestingSave : BaseTestCatchStoreTest
{
    [Fact]
    public async Task ItShouldListCatchImmediately_WhenSaved()
    {
        // Arrange
        var testCatch = new TestCatch(
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
        var testCatch = new TestCatch(
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
}
