namespace FishingLogBook.Web.Diagnostics;

public interface IDiagnosticEventStore
{
    Task EnqueueAsync(DiagnosticEvent diagnosticEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<DiagnosticEvent>> GetPendingAsync(int maxCount, CancellationToken cancellationToken);

    Task RemoveAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    Task<int> GetCountAsync(CancellationToken cancellationToken);

    Task<DiagnosticDatabaseInspection> InspectExistingAsync(CancellationToken cancellationToken);

    Task SaveAsync(DiagnosticEvent diagnosticEvent, CancellationToken cancellationToken);

    Task<StorageEstimate> GetStorageEstimateAsync(CancellationToken cancellationToken);
}
