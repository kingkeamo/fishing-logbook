using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage;

namespace FishingLogBook.Web.Features.Diagnostics.Services;

public interface IDiagnosticLogger
{
    Task LogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string>? metadata = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default);
}
