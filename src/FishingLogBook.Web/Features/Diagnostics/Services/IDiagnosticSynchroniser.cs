using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage;
namespace FishingLogBook.Web.Features.Diagnostics.Services;

public interface IDiagnosticSynchroniser
{
    Task SynchronisePendingAsync(CancellationToken cancellationToken);
}
