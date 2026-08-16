namespace FishingLogBook.Web.Diagnostics;

public interface IDiagnosticIndexedDbProbe
{
    Task<DiagnosticProbeResult> RunIsolatedAsync(CancellationToken cancellationToken);
}
