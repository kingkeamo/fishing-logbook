namespace FishingLogBook.Web.Diagnostics;

public sealed class DiagnosticProbeResult
{
    public string DatabaseName { get; init; } = string.Empty;

    public string? LastCompletedStage { get; set; }

    public string? FailedStage { get; set; }

    public string? Error { get; set; }

    public int? Count { get; set; }

    public bool Succeeded => FailedStage is null && LastCompletedStage is not null;
}
