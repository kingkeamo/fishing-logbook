using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.TripJsonTests;

public class WhenTestingRoundTrip
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    [Fact]
    public void ItShouldRejectUnreadableJson()
    {
        // Arrange
        // Act
        var act = () => TripJson.Deserialize("null");

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldWriteTheTripStatusAsThePascalCaseNameTheStoreCompares()
    {
        // Arrange
        var trip = NewTrip();

        // Act
        var json = TripJson.Serialize(trip);

        // Assert
        json.Should().Contain("\"status\":\"Active\"");
    }

    [Fact]
    public void ItShouldWriteTheSyncStatusAsACamelCaseNameMatchingTheCatchConvention()
    {
        // Arrange
        var trip = NewTrip() with { SyncStatus = SyncStatus.Synchronised };

        // Act
        var json = TripJson.Serialize(trip);

        // Assert
        json.Should().Contain("\"syncStatus\":\"synchronised\"");
    }

    [Fact]
    public void ItShouldRoundTripABlankActiveTrip()
    {
        // Arrange
        var trip = NewTrip();

        // Act
        var restored = TripJson.Deserialize(TripJson.Serialize(trip));

        // Assert
        restored.Should().Be(trip);
        restored.Title.Should().BeNull();
        restored.PlaceName.Should().BeNull();
        restored.Location.Should().BeNull();
        restored.EndedOn.Should().BeNull();
        restored.SyncedAt.Should().BeNull();
    }

    [Fact]
    public void ItShouldRoundTripACompletedTripWithATitleAndPlace()
    {
        // Arrange
        var trip = NewTrip() with
        {
            Status = TripConstants.Completed,
            EndedOn = StartedOn.AddHours(6),
            Title = "Day with Dad",
            PlaceName = "Lough Corrib"
        };

        // Act
        var restored = TripJson.Deserialize(TripJson.Serialize(trip));

        // Assert
        restored.Should().Be(trip);
        restored.Status.Should().Be(TripConstants.Completed);
        restored.EndedOn.Should().Be(StartedOn.AddHours(6));
    }

    [Fact]
    public void ItShouldRoundTripEveryLocationFieldIncludingProvenanceAndPrivacy()
    {
        // Arrange
        var trip = NewTrip() with
        {
            Location = new TripLocationModel(
                53.4419,
                -9.2531,
                8,
                StartedOn,
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion)
        };

        // Act
        var restored = TripJson.Deserialize(TripJson.Serialize(trip));

        // Assert
        restored.Location.Should().NotBeNull();
        restored.Location!.Latitude.Should().Be(53.4419);
        restored.Location.Longitude.Should().Be(-9.2531);
        restored.Location.AccuracyMetres.Should().Be(8);
        restored.Location.CapturedOn.Should().Be(StartedOn);
        restored.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        restored.Location.Visibility.Should().Be(LocationDefaults.Private);
        restored.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }

    [Fact]
    public void ItShouldRoundTripALocationWithNoAccuracy()
    {
        // Arrange
        var trip = NewTrip() with
        {
            Location = new TripLocationModel(
                53.4419,
                -9.2531,
                null,
                StartedOn,
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion)
        };

        // Act
        var restored = TripJson.Deserialize(TripJson.Serialize(trip));

        // Assert
        restored.Location!.AccuracyMetres.Should().BeNull();
    }

    [Fact]
    public void ItShouldPreserveTheOffsetOnStoredInstants()
    {
        // Arrange
        var startedOn = DateTimeOffset.Parse("2026-08-26T06:32:00+01:00");
        var trip = NewTrip() with { StartedOn = startedOn };

        // Act
        var restored = TripJson.Deserialize(TripJson.Serialize(trip));

        // Assert
        restored.StartedOn.Should().Be(startedOn);
        restored.StartedOn.ToUniversalTime().Should().Be(startedOn.ToUniversalTime());
    }

    [Fact]
    public void ItShouldNotSubstituteTheCurrentTimeForAMissingEnd()
    {
        // Arrange
        var json = TripJson.Serialize(NewTrip());

        // Act
        var restored = TripJson.Deserialize(json);

        // Assert
        restored.EndedOn.Should().BeNull();
    }

    [Fact]
    public void ItShouldRoundTripTheSyncState()
    {
        // Arrange
        var syncedAt = StartedOn.AddHours(7);
        var trip = NewTrip() with
        {
            SyncStatus = SyncStatus.Synchronised,
            SyncedAt = syncedAt
        };

        // Act
        var restored = TripJson.Deserialize(TripJson.Serialize(trip));

        // Assert
        restored.SyncStatus.Should().Be(SyncStatus.Synchronised);
        restored.SyncedAt.Should().Be(syncedAt);
    }

    private static TripModel NewTrip()
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn);
    }
}
