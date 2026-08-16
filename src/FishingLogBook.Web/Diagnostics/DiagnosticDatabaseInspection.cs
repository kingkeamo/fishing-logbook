namespace FishingLogBook.Web.Diagnostics;

public sealed class DiagnosticDatabaseInspection
{
    public bool Exists { get; init; }

    public bool HasStore { get; init; }

    public int Count { get; init; }
}
