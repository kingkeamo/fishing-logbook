using System.Text.Json;
using System.Text.Json.Serialization;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Offline;

internal static class CatchJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string SerializeMetadata(CatchModel catchRecord)
    {
        var metadata = new CatchMetadata(
            catchRecord.Id,
            catchRecord.CaughtOn,
            catchRecord.SpeciesName,
            catchRecord.Photographs
                .Select(photograph => new CatchPhotographMetadata(
                    photograph.Id,
                    photograph.CatchId,
                    photograph.ContentType,
                    photograph.SyncStatus,
                    photograph.ObjectKey))
                .ToArray(),
            catchRecord.Location,
            catchRecord.UserId,
            catchRecord.SyncStatus,
            catchRecord.MetadataSyncStatus,
            catchRecord.AnglerUserId,
            catchRecord.RecordedByUserId,
            catchRecord.Weight,
            catchRecord.Length,
            catchRecord.Method,
            catchRecord.BaitOrLure,
            catchRecord.Notes,
            catchRecord.SyncedAt);
        return JsonSerializer.Serialize(metadata, Options);
    }

    public static CatchModel Deserialize(string json, IReadOnlyList<CatchPhotographModel> photographs)
    {
        var metadata = JsonSerializer.Deserialize<CatchMetadata>(json, Options)
            ?? throw new InvalidOperationException("Catch metadata could not be read.");
        return ToModel(metadata, OrderPhotographs(metadata.Photographs, photographs));
    }

    private static CatchModel ToModel(
        CatchMetadata metadata,
        IReadOnlyList<CatchPhotographModel> photographs)
    {
        return new CatchModel(
            metadata.Id,
            metadata.CaughtOn,
            photographs,
            metadata.SpeciesName,
            metadata.Location,
            metadata.UserId,
            metadata.SyncStatus,
            metadata.MetadataSyncStatus,
            metadata.AnglerUserId == Guid.Empty ? metadata.UserId : metadata.AnglerUserId,
            metadata.RecordedByUserId == Guid.Empty ? metadata.UserId : metadata.RecordedByUserId,
            metadata.Weight,
            metadata.Length,
            metadata.Method,
            metadata.BaitOrLure,
            metadata.Notes,
            metadata.SyncedAt);
    }

    public static CatchModel DeserializeMetadata(string json)
    {
        var metadata = JsonSerializer.Deserialize<CatchMetadata>(json, Options)
            ?? throw new InvalidOperationException("Catch metadata could not be read.");
        var photographs = metadata.Photographs
            .Select(photograph => new CatchPhotographModel(
                photograph.Id,
                photograph.CatchId,
                photograph.ContentType,
                Bytes: null,
                photograph.SyncStatus,
                photograph.ObjectKey))
            .ToArray();
        return ToModel(metadata, photographs);
    }

    private static IReadOnlyList<CatchPhotographModel> OrderPhotographs(
        IReadOnlyList<CatchPhotographMetadata> metadataPhotographs,
        IReadOnlyList<CatchPhotographModel> photographs)
    {
        var byId = photographs.ToDictionary(photograph => photograph.Id);
        var ordered = new List<CatchPhotographModel>(photographs.Count);
        foreach (var metadata in metadataPhotographs)
        {
            if (byId.Remove(metadata.Id, out var photograph))
            {
                ordered.Add(photograph with
                {
                    SyncStatus = metadata.SyncStatus,
                    ObjectKey = metadata.ObjectKey
                });
            }
        }

        ordered.AddRange(byId.Values);
        return ordered;
    }

    private sealed record CatchMetadata(
        Guid Id,
        DateTimeOffset CaughtOn,
        string? SpeciesName,
        IReadOnlyList<CatchPhotographMetadata> Photographs,
        CatchLocationModel? Location = null,
        Guid UserId = default,
        SyncStatus SyncStatus = SyncStatus.SavedLocally,
        SyncStatus MetadataSyncStatus = SyncStatus.SavedLocally,
        Guid AnglerUserId = default,
        Guid RecordedByUserId = default,
        decimal? Weight = null,
        decimal? Length = null,
        string? Method = null,
        string? BaitOrLure = null,
        string? Notes = null,
        DateTimeOffset? SyncedAt = null);

    private sealed record CatchPhotographMetadata(
        Guid Id,
        Guid CatchId,
        string ContentType,
        SyncStatus SyncStatus = SyncStatus.SavedLocally,
        string? ObjectKey = null);
}
