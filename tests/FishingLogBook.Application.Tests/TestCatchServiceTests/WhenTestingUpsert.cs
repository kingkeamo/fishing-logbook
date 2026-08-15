using AwesomeAssertions;
using FishingLogBook.Application.TestCatches;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Tests.TestCatchServiceTests;

public class WhenTestingUpsert
{
    [Fact]
    public async Task ItShouldKeepASingleRecord_WhenTheSameCatchIsUpsertedTwice()
    {
        // Arrange
        var repository = new MemoryTestCatchRepository();
        var sut = new TestCatchService(repository, new MemoryObjectStorage());
        var testCatch = new TestCatchDto(
            Guid.Parse("4e2a1c90-8b33-4f6d-9a17-5c0e8d2b1a44"),
            "Pike",
            DateTimeOffset.Parse("2026-08-14T12:00:00Z"),
            "First attempt");

        // Act
        await sut.UpsertAsync(testCatch, CancellationToken.None);
        var retried = testCatch with { Notes = "Retry after timeout" };
        await sut.UpsertAsync(retried, CancellationToken.None);
        var listed = await sut.ListAsync(CancellationToken.None);

        // Assert
        listed.Should().ContainSingle()
            .Which.Id.Should().Be(testCatch.Id);
        listed[0].Notes.Should().Be("First attempt");
    }

    [Fact]
    public async Task ItShouldClearLocation_WhenUpsertedWithoutLocation()
    {
        // Arrange
        var repository = new MemoryTestCatchRepository();
        var sut = new TestCatchService(repository, new MemoryObjectStorage());
        var id = Guid.Parse("7c3e1a90-2b44-4f6d-8a17-5c0e8d2b1a55");
        var withLocation = new TestCatchDto(
            id,
            "Pike",
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            null,
            Location: new CatchLocationDto(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));

        // Act
        await sut.UpsertAsync(withLocation, CancellationToken.None);
        await sut.UpsertAsync(withLocation with { Location = null }, CancellationToken.None);
        var listed = await sut.ListAsync(CancellationToken.None);

        // Assert
        listed.Should().ContainSingle()
            .Which.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnTheCatch_WhenUpserted()
    {
        // Arrange
        var sut = new TestCatchService(new MemoryTestCatchRepository(), new MemoryObjectStorage());
        var testCatch = new TestCatchDto(
            Guid.NewGuid(),
            "Perch",
            DateTimeOffset.Parse("2026-08-14T13:00:00Z"),
            null);

        // Act
        var saved = await sut.UpsertAsync(testCatch, CancellationToken.None);

        // Assert
        saved.Should().Be(testCatch);
    }

    [Fact]
    public async Task ItShouldPersistLocation_WhenUpserted()
    {
        // Arrange
        var sut = new TestCatchService(new MemoryTestCatchRepository(), new MemoryObjectStorage());
        var location = new CatchLocationDto(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var testCatch = new TestCatchDto(
            Guid.NewGuid(),
            "Pike",
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            null,
            Location: location);

        // Act
        var saved = await sut.UpsertAsync(testCatch, CancellationToken.None);
        var listed = await sut.ListAsync(CancellationToken.None);

        // Assert
        saved.Location.Should().Be(location);
        listed.Should().ContainSingle()
            .Which.Location.Should().Be(location);
    }
}
