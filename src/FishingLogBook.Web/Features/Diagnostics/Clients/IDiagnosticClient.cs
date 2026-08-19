using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage;

namespace FishingLogBook.Web.Features.Diagnostics.Clients;

public interface IDiagnosticClient
{
    Task UploadBatchAsync(IReadOnlyList<ClientDiagnosticEventDto> events, CancellationToken cancellationToken);
}
