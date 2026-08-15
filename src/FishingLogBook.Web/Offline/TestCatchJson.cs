using System.Text.Json;
using System.Text.Json.Serialization;

namespace FishingLogBook.Web.Offline;

internal static class TestCatchJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
