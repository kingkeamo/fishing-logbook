using AwesomeAssertions;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;

namespace FishingLogBook.Web.Tests.TestCatchStoreTests;

public class WhenTestingNewestFirst : BaseTestCatchStoreTest
{
    [Fact]
    public async Task ItShouldReturnTheNewestCatchFirst()
    {
        // Arrange
        var older = new TestCatch(
            Guid.Parse("3a1f2c8e-7b44-4d1a-9c3e-2f8a6b0d1e55"),
            "Perch",
            DateTimeOffset.Parse("2026-08-14T10:00:00Z"),
            null,
            SyncStatus.SavedLocally);
        var newer = new TestCatch(
            Guid.Parse("8c0e91a2-4d77-4b18-a6f1-0c3d5e7a9b21"),
            "Pike",
            DateTimeOffset.Parse("2026-08-15T08:30:00Z"),
            null,
            SyncStatus.SavedLocally);
        await Sut.SaveAsync(older, CancellationToken.None);
        await Sut.SaveAsync(newer, CancellationToken.None);

        // Act
        var saved = await Sut.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().HaveCount(2);
        saved[0].Id.Should().Be(newer.Id);
        saved[1].Id.Should().Be(older.Id);
    }
}
