using System.Text.Json;
using System.Text.Json.Serialization;
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
                    photograph.ContentType))
                .ToArray());
        return JsonSerializer.Serialize(metadata, Options);
    }

    public static CatchModel Deserialize(string json, IReadOnlyList<CatchPhotographModel> photographs)
    {
        var metadata = JsonSerializer.Deserialize<CatchMetadata>(json, Options)
            ?? throw new InvalidOperationException("Catch metadata could not be read.");
        return new CatchModel(metadata.Id, metadata.CaughtOn, photographs, metadata.SpeciesName);
    }

    private sealed record CatchMetadata(
        Guid Id,
        DateTimeOffset CaughtOn,
        string? SpeciesName,
        IReadOnlyList<CatchPhotographMetadata> Photographs);

    private sealed record CatchPhotographMetadata(
        Guid Id,
        Guid CatchId,
        string ContentType);
}
