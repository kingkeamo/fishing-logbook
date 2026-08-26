using System.Text.Json;
using System.Text.Json.Serialization;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Offline;

internal static class TripJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(TripModel trip)
    {
        return JsonSerializer.Serialize(ToRecord(trip), Options);
    }

    public static TripModel Deserialize(string json)
    {
        var record = JsonSerializer.Deserialize<StoredTrip>(json, Options)
            ?? throw new InvalidOperationException("Trip metadata could not be read.");
        return new TripModel(
            record.Id,
            record.OwnerUserId,
            record.Status,
            record.StartedOn,
            record.EndedOn,
            record.Title,
            record.PlaceName,
            ToLocation(record.Location),
            record.SyncStatus,
            record.SyncedAt);
    }

    private static StoredTrip ToRecord(TripModel trip)
    {
        return new StoredTrip(
            trip.Id,
            trip.OwnerUserId,
            trip.Status,
            trip.StartedOn,
            trip.EndedOn,
            trip.Title,
            trip.PlaceName,
            trip.Location is null
                ? null
                : new StoredTripLocation(
                    trip.Location.Latitude,
                    trip.Location.Longitude,
                    trip.Location.AccuracyMetres,
                    trip.Location.CapturedOn,
                    trip.Location.Source,
                    trip.Location.Visibility,
                    trip.Location.ConsentVersion),
            trip.SyncStatus,
            trip.SyncedAt);
    }

    private static TripLocationModel? ToLocation(StoredTripLocation? location)
    {
        if (location is null)
        {
            return null;
        }

        return new TripLocationModel(
            location.Latitude,
            location.Longitude,
            location.AccuracyMetres,
            location.CapturedOn,
            location.Source,
            location.Visibility,
            location.ConsentVersion);
    }

    private sealed record StoredTrip(
        Guid Id,
        Guid OwnerUserId,
        string Status,
        DateTimeOffset StartedOn,
        DateTimeOffset? EndedOn = null,
        string? Title = null,
        string? PlaceName = null,
        StoredTripLocation? Location = null,
        SyncStatus SyncStatus = SyncStatus.SavedLocally,
        DateTimeOffset? SyncedAt = null);

    private sealed record StoredTripLocation(
        double Latitude,
        double Longitude,
        double? AccuracyMetres,
        DateTimeOffset CapturedOn,
        string Source,
        string Visibility,
        string ConsentVersion);
}
