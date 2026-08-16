namespace FishingLogBook.Web.Features.Diagnostics.Models;

public sealed class DiagnosticDatabaseInspectionModel
{
    public bool Exists { get; init; }

    public bool HasStore { get; init; }

    public int Count { get; init; }
}
