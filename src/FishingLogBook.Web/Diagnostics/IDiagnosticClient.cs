using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Diagnostics;

public interface IDiagnosticClient
{
    Task UploadBatchAsync(IReadOnlyList<ClientDiagnosticEventDto> events, CancellationToken cancellationToken);
}
