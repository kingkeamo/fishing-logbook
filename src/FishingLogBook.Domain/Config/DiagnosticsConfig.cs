namespace FishingLogBook.Domain.Config;

public sealed class DiagnosticsConfig
{
    public const string SectionName = "Diagnostics";

    public int MaxBatchSize { get; set; } = 50;

    public int MaxMessageLength { get; set; } = 1000;

    public int MaxStackTraceLength { get; set; } = 2000;

    public int MaxEventNameLength { get; set; } = 80;

    public string MinimumLevel { get; set; } = "Warning";
}
