using FishingLogBook.Web.Features.Diagnostics.Models;
namespace FishingLogBook.Web.Features.Diagnostics.Storage;

public interface IDiagnosticIndexedDbProbe
{
    Task<DiagnosticProbeResultModel> RunIsolatedAsync(CancellationToken cancellationToken);
}
