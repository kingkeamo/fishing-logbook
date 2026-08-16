using AwesomeAssertions;
using FishingLogBook.Domain.TestCatches;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.TestCatches.TestCatchServiceTests;

public class WhenTestingUpsert : BaseTestCatchServiceTest
{
    [Fact]
    public async Task ItShouldPassTheRetryPayloadToTheRepository()
    {
        // Arrange
        var testCatch = new TestCatchDto(
            Guid.Parse("4e2a1c90-8b33-4f6d-9a17-5c0e8d2b1a44"),
            "Pike",
            DateTimeOffset.Parse("2026-08-14T12:00:00Z"),
            "First attempt");

        // Act
        await Sut.UpsertAsync(testCatch, CancellationToken.None);
        var retried = testCatch with { Notes = "Retry after timeout" };
        await Sut.UpsertAsync(retried, CancellationToken.None);

        // Assert
        await MockTestCatchRepository.Received(1).UpsertAsync(
            Arg.Is<TestCatchRecord>(record =>
                record.Id == testCatch.Id &&
                record.SpeciesName == "Pike" &&
                record.Notes == "First attempt"),
            Arg.Any<CancellationToken>());
        await MockTestCatchRepository.Received(1).UpsertAsync(
            Arg.Is<TestCatchRecord>(record =>
                record.Id == testCatch.Id &&
                record.SpeciesName == "Pike" &&
                record.Notes == "Retry after timeout"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearLocation_WhenUpsertedWithoutLocation()
    {
        // Arrange
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
        MockTestCatchRepository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<TestCatchRecord>>(
            [
                new TestCatchRecord
                {
                    Id = id,
                    SpeciesName = "Pike",
                    CaughtOn = withLocation.CaughtOn
                }
            ]));

        // Act
        await Sut.UpsertAsync(withLocation, CancellationToken.None);
        await Sut.UpsertAsync(withLocation with { Location = null }, CancellationToken.None);
        var listed = await Sut.ListAsync(CancellationToken.None);

        // Assert
        listed.Should().ContainSingle()
            .Which.Location.Should().BeNull();
        await MockTestCatchRepository.Received(1).UpsertAsync(
            Arg.Is<TestCatchRecord>(record =>
                record.Id == id &&
                record.Latitude == 53.2707 &&
                record.Longitude == -9.0568),
            Arg.Any<CancellationToken>());
        await MockTestCatchRepository.Received(1).UpsertAsync(
            Arg.Is<TestCatchRecord>(record =>
                record.Id == id &&
                record.Latitude == null &&
                record.Longitude == null),
            Arg.Any<CancellationToken>());
        await MockTestCatchRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheCatch_WhenUpserted()
    {
        // Arrange
        var testCatch = new TestCatchDto(
            Guid.NewGuid(),
            "Perch",
            DateTimeOffset.Parse("2026-08-14T13:00:00Z"),
            null);

        // Act
        var saved = await Sut.UpsertAsync(testCatch, CancellationToken.None);

        // Assert
        saved.Should().Be(testCatch);
        await MockTestCatchRepository.Received(1).UpsertAsync(
            Arg.Is<TestCatchRecord>(record =>
                record.Id == testCatch.Id &&
                record.SpeciesName == "Perch" &&
                record.Notes == null &&
                record.Latitude == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistLocation_WhenUpserted()
    {
        // Arrange
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
        MockTestCatchRepository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchRecord>>(
            [
                new TestCatchRecord
                {
                    Id = testCatch.Id,
                    SpeciesName = testCatch.SpeciesName,
                    CaughtOn = testCatch.CaughtOn,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    LocationAccuracyMetres = location.AccuracyMetres,
                    LocationCapturedOn = location.CapturedOn,
                    LocationSource = location.Source,
                    LocationVisibility = location.Visibility,
                    LocationConsentVersion = location.ConsentVersion
                }
            ]));

        // Act
        var saved = await Sut.UpsertAsync(testCatch, CancellationToken.None);
        var listed = await Sut.ListAsync(CancellationToken.None);

        // Assert
        saved.Location.Should().Be(location);
        listed.Should().ContainSingle()
            .Which.Location.Should().Be(location);
        await MockTestCatchRepository.Received(1).UpsertAsync(
            Arg.Is<TestCatchRecord>(record =>
                record.Id == testCatch.Id &&
                record.SpeciesName == "Pike" &&
                record.Latitude == location.Latitude &&
                record.Longitude == location.Longitude &&
                record.LocationVisibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
        await MockTestCatchRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
