using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Web.Features.Diagnostics.Models;

public sealed class DiagnosticStatusModel
{
    public int QueuedCount { get; set; }

    public bool QueueCountAvailable { get; set; }

    public string? LastOperation { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? LastErrorAtUtc { get; set; }

    public long? StorageUsageBytes { get; set; }

    public long? StorageQuotaBytes { get; set; }

    public bool? IsOnline { get; set; }

    public void RecordSuccess(string operation)
    {
        LastOperation = operation;
    }

    public void RecordQueueCount(int count)
    {
        QueuedCount = count;
        QueueCountAvailable = true;
    }

    public void MarkQueueCountUnavailable()
    {
        QueueCountAvailable = false;
    }

    public void RecordFailure(string operation, Exception exception)
    {
        LastOperation = operation;
        LastErrorAtUtc = DateTimeOffset.UtcNow;
        var message = DiagnosticMetadata.SafeErrorMessage(exception.Message, 120);
        LastError = string.IsNullOrWhiteSpace(message)
            ? $"{operation}: {exception.GetType().Name}"
            : $"{operation}: {exception.GetType().Name}: {message}";
    }
}
