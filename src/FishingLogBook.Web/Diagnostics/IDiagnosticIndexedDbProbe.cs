namespace FishingLogBook.Web.Diagnostics;

public interface IDiagnosticIndexedDbProbe
{
    Task<DiagnosticProbeResult> RunAsync(
        string databaseName,
        bool writeTestRecord,
        CancellationToken cancellationToken);
}
