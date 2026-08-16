using System.Text.Json;
using System.Text.Json.Serialization;
using FishingLogBook.Web.Features.TestCatch.Models;

namespace FishingLogBook.Web.Features.TestCatch.Offline;

internal static class TestCatchJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
