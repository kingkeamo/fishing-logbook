using System.Text.Json;
using System.Text.Json.Serialization;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
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
            record.SyncedAt,
            [.. record.Photographs.Select(ToPhotograph)],
            [.. record.Notes.Select(ToNote)],
            record.ParticipantUserIds,
            record.Origin);
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
            trip.SyncedAt,
            [.. (trip.Photographs ?? []).Select(ToStoredPhotograph)],
            [.. (trip.Notes ?? []).Select(ToStoredNote)],
            trip.ParticipantUserIds,
            trip.Origin);
    }

    public static string SerializeNote(TripNoteModel note)
    {
        return JsonSerializer.Serialize(ToStoredNote(note), Options);
    }

    public static TripNoteModel DeserializeNote(string json)
    {
        var record = JsonSerializer.Deserialize<StoredTripNote>(json, Options)
            ?? throw new InvalidOperationException("Trip note metadata could not be read.");
        return ToNote(record);
    }

    private static StoredTripNote ToStoredNote(TripNoteModel note)
    {
        return new StoredTripNote(
            note.Id,
            note.TripId,
            note.CreatedByUserId,
            note.Text,
            note.RecordedOn,
            note.SyncStatus,
            note.SyncedAt);
    }

    private static TripNoteModel ToNote(StoredTripNote record)
    {
        return new TripNoteModel(
            record.Id,
            record.TripId,
            record.Author,
            record.Text,
            record.RecordedOn,
            record.SyncStatus,
            record.SyncedAt);
    }

    public static string SerializePhotograph(TripPhotographModel photograph)
    {
        return JsonSerializer.Serialize(ToStoredPhotograph(photograph), Options);
    }

    public static TripPhotographModel DeserializePhotograph(string json)
    {
        var record = JsonSerializer.Deserialize<StoredTripPhotograph>(json, Options)
            ?? throw new InvalidOperationException("Trip photograph metadata could not be read.");
        return ToPhotograph(record);
    }

    private static StoredTripPhotograph ToStoredPhotograph(TripPhotographModel photograph)
    {
        return new StoredTripPhotograph(
            photograph.Id,
            photograph.TripId,
            photograph.ContributedByUserId,
            photograph.ContentType,
            photograph.AddedOn,
            photograph.CapturedOn,
            photograph.ObjectKey,
            photograph.SyncStatus,
            photograph.SyncedAt);
    }

    private static TripPhotographModel ToPhotograph(StoredTripPhotograph record)
    {
        return new TripPhotographModel(
            record.Id,
            record.TripId,
            record.Contributor,
            record.ContentType,
            record.AddedOn,
            record.CapturedOn,
            Bytes: null,
            record.ObjectKey,
            record.SyncStatus,
            record.SyncedAt);
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
        DateTimeOffset? SyncedAt = null,
        IReadOnlyList<StoredTripPhotograph>? Photographs = null,
        IReadOnlyList<StoredTripNote>? Notes = null,
        IReadOnlyList<Guid>? ParticipantUserIds = null,
        TripOriginEnum Origin = TripOriginEnum.Local)
    {
        public IReadOnlyList<StoredTripPhotograph> Photographs { get; init; } = Photographs ?? [];

        public IReadOnlyList<StoredTripNote> Notes { get; init; } = Notes ?? [];

        public IReadOnlyList<Guid> ParticipantUserIds { get; init; } = ParticipantUserIds ?? [];
    }

    private sealed record StoredTripNote(
        Guid Id,
        Guid TripId,
        Guid CreatedByUserId,
        string Text,
        DateTimeOffset RecordedOn,
        SyncStatus SyncStatus = SyncStatus.SavedLocally,
        DateTimeOffset? SyncedAt = null)
    {
        public Guid OwnerUserId { get; init; }

        public Guid Author
        {
            get
            {
                return CreatedByUserId == Guid.Empty ? OwnerUserId : CreatedByUserId;
            }
        }
    }

    private sealed record StoredTripPhotograph(
        Guid Id,
        Guid TripId,
        Guid ContributedByUserId,
        string ContentType,
        DateTimeOffset AddedOn,
        DateTimeOffset? CapturedOn = null,
        string? ObjectKey = null,
        SyncStatus SyncStatus = SyncStatus.SavedLocally,
        DateTimeOffset? SyncedAt = null)
    {
        public Guid OwnerUserId { get; init; }

        public Guid Contributor
        {
            get
            {
                return ContributedByUserId == Guid.Empty ? OwnerUserId : ContributedByUserId;
            }
        }
    }

    private sealed record StoredTripLocation(
        double Latitude,
        double Longitude,
        double? AccuracyMetres,
        DateTimeOffset CapturedOn,
        string Source,
        string Visibility,
        string ConsentVersion);
}
