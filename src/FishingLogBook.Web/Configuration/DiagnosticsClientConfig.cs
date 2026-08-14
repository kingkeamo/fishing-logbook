namespace FishingLogBook.Web.Configuration;

public sealed class DiagnosticsClientConfig
{
    public const string SectionName = "Diagnostics";

    public bool ShowInspector { get; set; }

    public string MinimumPersistLevel { get; set; } = "Warning";

    public int MaxQueueSize { get; set; } = 500;

    public int MaxBatchSize { get; set; } = 50;

    public int OperationTimeoutMilliseconds { get; set; } = 5000;

    public int MaxUploadAttempts { get; set; } = 5;

    public TimeSpan OperationTimeout => TimeSpan.FromMilliseconds(Math.Max(250, OperationTimeoutMilliseconds));
}
