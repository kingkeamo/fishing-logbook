namespace FishingLogBook.Web.Diagnostics;

public interface IDiagnosticSynchroniser
{
    Task SynchronisePendingAsync(CancellationToken cancellationToken);
}
