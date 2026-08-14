namespace FishingLogBook.Web.Diagnostics;

public sealed class DiagnosticStatus
{
    public int QueuedCount { get; set; }

    public string? LastError { get; set; }

    public long? StorageUsageBytes { get; set; }

    public long? StorageQuotaBytes { get; set; }

    public bool? IsOnline { get; set; }
}
