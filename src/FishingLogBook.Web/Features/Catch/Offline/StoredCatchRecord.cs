namespace FishingLogBook.Web.Features.Catch.Offline;

internal sealed class StoredCatchPhotographRecord
{
    public string Id { get; set; } = string.Empty;

    public string CatchId { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string BytesBase64 { get; set; } = string.Empty;
}

internal sealed class StoredCatchRecord
{
    public string Json { get; set; } = string.Empty;

    public StoredCatchPhotographRecord[] Photographs { get; set; } = [];
}

internal sealed class StoredCatchPhotographWrite
{
    public string Id { get; set; } = string.Empty;

    public string CatchId { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public byte[] Bytes { get; set; } = [];
}
