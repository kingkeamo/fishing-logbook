using System.Text.Json;

namespace FishingLogBook.Shared.Tests.Serialization;

public class BaseSerializationTest
{
    protected static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
}
