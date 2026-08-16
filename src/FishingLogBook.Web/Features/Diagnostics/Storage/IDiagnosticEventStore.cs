using FishingLogBook.Web.Features.Diagnostics.Models;
namespace FishingLogBook.Web.Features.Diagnostics.Storage;

public interface IDiagnosticEventStore
{
    Task EnqueueAsync(DiagnosticEventModel diagnosticEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<DiagnosticEventModel>> GetPendingAsync(int maxCount, CancellationToken cancellationToken);

    Task RemoveAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    Task<int> GetCountAsync(CancellationToken cancellationToken);

    Task<DiagnosticDatabaseInspectionModel> InspectExistingAsync(CancellationToken cancellationToken);

    Task SaveAsync(DiagnosticEventModel diagnosticEvent, CancellationToken cancellationToken);

    Task<StorageEstimate> GetStorageEstimateAsync(CancellationToken cancellationToken);
}
